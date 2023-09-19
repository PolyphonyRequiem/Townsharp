using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Websockets;

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


    protected sealed override void HandleMessage(string message)
    {
        try
        {
            using (var document = JsonDocument.Parse(message))
            {
                if (this.IsError(document))
                {
                    // Error
                    return;
                }

                TMessage m = this.ToMessage(document);
                if (this.IsResponse(m))
                {
                    TResponseMessage responseMessage = this.ToResponseMessage(document);

                    var id = this.GetResponseId(responseMessage);

                    if (this.pendingRequests.TryRemove(id, out var tcs))
                    {
                        tcs.SetResult(responseMessage);
                    }
                    else
                    {
                        base.logger.LogWarning($"Received response with id {id} but no pending request was found.");
                    }

                    return;
                }

                if (this.IsEvent(m))
                {
                    var @event = this.ToEventMessage(document);
                    this.HandleEvent(@event);
                    return;
                }
            }
        }
        catch(JsonException ex)
        {
            // Error?
            if (this.IsError(message))
            {
                base.logger.LogError($"Received error message: {message}");
                return;
            }

            // Error.
            this.logger.LogError(ex, $"Failed to parse message: {message}");
        }        
    }

    protected abstract bool IsError(string message);

    protected abstract bool IsError(JsonDocument document);

    protected abstract bool IsResponse(TMessage message);

    protected abstract bool IsEvent(TMessage message);

    protected abstract TMessage ToMessage(JsonDocument document);

    protected abstract TResponseMessage ToResponseMessage(JsonDocument document);

    protected abstract TEventMessage ToEventMessage(JsonDocument message);
        
    protected abstract int GetResponseId(TResponseMessage responseMessage);

    protected abstract void HandleEvent(TEventMessage @event);
}
