using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Websockets;

// We should REALLY move this to composition for both testing and accessibility modifier reasons.

internal abstract class RequestsAndEventsWebsocketClient<TMessage, TResponseMessage, TEventMessage> : WebsocketMessageClient
    where TMessage : class
    where TEventMessage : class
    where TResponseMessage : class
{
    // State
    private readonly ConcurrentDictionary<int, TaskCompletionSource<TResponseMessage>> pendingRequests = new();
    private readonly SemaphoreSlim sendSemaphore;

    // Dependencies
    private readonly MessageIdFactory messageIdFactory;

    internal RequestsAndEventsWebsocketClient(ILogger logger, int maxRequests) : base(logger)
    {
        this.messageIdFactory = new MessageIdFactory();
        
        if (maxRequests < 1 || maxRequests > 80)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "maxRequests must be between 1 and 100");
        }

        this.sendSemaphore = new SemaphoreSlim((int)maxRequests);
    }

    protected async IAsyncEnumerable<TEventMessage> ReceiveEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Mixing the concepts of events and messages here by deferring the call for receiveeventsasync to the calling class.
        // Issuing requests such as migration will fail if we are not receiving events. 
        // This could easily result in astonishing behavior.
        // It probably makes sense to split these two concepts up and use channel or similar to communicate between the "on connected" message handler, and the event handler.
        // Consider lifecycle implications of this.
        // Most likely we should make event handling subordinate to the message handling.
        await foreach (var message in base.ListenForMessagesAsync(cts.Token))
        {
            ErrorInfo errorInfo = ErrorInfo.None;
            TEventMessage? eventMessage = default;

            try
            {
                using (var document = JsonDocument.Parse(message))
                {
                    errorInfo = this.CheckForError(document);
                    if (errorInfo.IsError)
                    {
                        if (errorInfo.ErrorType == ErrorType.FatalError)
                        {
                            cts.Cancel();
                            yield break;
                        }
                    }

                    TMessage m = this.ToMessage(document);
                    if (this.IsResponse(m))
                    {
                        TResponseMessage responseMessage = this.ToResponseMessage(document);

                        errorInfo = this.CheckResponseForError(responseMessage);

                        if (errorInfo.IsError)
                        {
                            if (errorInfo.ErrorType == ErrorType.FatalError)
                            {
                                base.logger.LogError($"A fatal error has occurred while processing the response. {errorInfo.ErrorMessage}");
                                cts.Cancel();
                                yield break;
                            }
                            else
                            {
                                base.logger.LogError($"A recoverable error has occurred while processing the response. {errorInfo.ErrorMessage}");
                            }
                        }
                        
                        var id = this.GetResponseId(responseMessage);

                        if (this.pendingRequests.TryRemove(id, out var tcs))
                        {
                            tcs.SetResult(responseMessage);
                        }
                        else
                        {
                            base.logger.LogWarning($"Received response with id {id} but no pending request was found.");
                        }                        
                    }

                    if (this.IsEvent(m))
                    {
                        eventMessage = this.ToEventMessage(document);
                    }
                }
            }
            catch (JsonException ex)
            {
                // Do we have a concrete error in the raw text? If so let's use that.
                errorInfo = this.CheckForError(message);

                if (!errorInfo.IsError)
                {
                    errorInfo = new ErrorInfo(ErrorType.FatalError, ex.ToString());
                }
            }

            if (errorInfo.IsError)
            {
                if (errorInfo.ErrorType == ErrorType.FatalError)
                {
                    this.logger.LogError($"A fatal error occurred while processing message {message}. ErrorMessage: {errorInfo.ErrorMessage}");
                    cts.Cancel();
                    yield break;
                }
                else
                {
                    this.logger.LogWarning($"A recoverable error occurred while processing message {message}. ErrorType: {errorInfo.ErrorType} ErrorMessage: {errorInfo.ErrorMessage}");
                }
            }

            if (eventMessage != default)
            {
                yield return eventMessage;
            }
        }
    }

    protected async Task<Response<TResponseMessage>> SendRequestAsync(Func<int, string> requestMessageFactory, TimeSpan timeout)
    {
        var id = this.messageIdFactory.GetNextId();
        var requestMessage = requestMessageFactory(id);

        TaskCompletionSource<TResponseMessage> tcs = new TaskCompletionSource<TResponseMessage>();
        await this.sendSemaphore.WaitAsync();

        Task sendTask = base.SendMessageAsync(requestMessage)
             .ContinueWith(task =>
             {
                 this.sendSemaphore.Release();
                 if (task.IsFaulted)
                 {
                     tcs.SetException(task.Exception!.InnerExceptions);
                 }
                 else if (task.IsCanceled)
                 {
                     tcs.SetCanceled();
                 }
             });

        if (!this.pendingRequests.TryAdd(id, tcs))
        {
            tcs.SetException(new InvalidOperationException("Failed to add request to pending requests"));
        }

        var result = tcs.Task.WaitAsync(timeout)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    if (task.Exception?.InnerExceptions.Any(e => e is TimeoutException) ?? false)
                    {
                        return Response<TResponseMessage>.Timeout();
                    }
                    else
                    {
                        return Response<TResponseMessage>.Error(task.Exception?.ToString() ?? "Unknown Error Occurred.");
                    }
                }
                else if (task.IsCanceled)
                {
                    return Response<TResponseMessage>.Cancelled();
                }

                return Response<TResponseMessage>.Completed(task.Result);
            });

        try
        {
            await sendTask; // just to close the loop.
            var response = await result.ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            base.logger.LogError(ex, $"Failed to send request {requestMessage}");
            throw;
        }
        finally
        {
            this.pendingRequests.TryRemove(id, out _);
        }
    }

    protected abstract ErrorInfo CheckForError(string message);

    protected abstract ErrorInfo CheckForError(JsonDocument document);

    protected abstract ErrorInfo CheckResponseForError(TResponseMessage response);

    protected abstract bool IsResponse(TMessage message);

    protected abstract bool IsEvent(TMessage message);

    protected abstract TMessage ToMessage(JsonDocument document);

    protected abstract TResponseMessage ToResponseMessage(JsonDocument document);

    protected abstract TEventMessage ToEventMessage(JsonDocument message);
        
    protected abstract int GetResponseId(TResponseMessage responseMessage);

    protected enum ErrorType
    {
        NoError,
        InfrastructureError,
        UserError,
        ServiceError,
        TimeoutError,
        FatalError
    }

    protected record ErrorInfo(ErrorType ErrorType, string? ErrorMessage)
    {
        public bool IsError => this.ErrorType != ErrorType.NoError;

        public static ErrorInfo None => new(ErrorType.NoError, null);
    }
}
