using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.ServerConsole;

public class ConsoleSession
{
    // Constants
    public static int MAX_CONCURRENT_REQUESTS = 20;
    private static readonly TimeSpan IdleConnectionTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan IdleConnectionCheckPeriod = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(30);

    // Logging
    private readonly ILogger<ConsoleSession> logger;

    // State
    private DateTimeOffset lastMessage = DateTimeOffset.UtcNow;
    private readonly TaskCompletionSource<bool> authenticatedTcs = new TaskCompletionSource<bool>();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode>> pendingRequests = new();
    private readonly SemaphoreSlim sendSemaphore = new SemaphoreSlim(MAX_CONCURRENT_REQUESTS);

    // Buffers
    private readonly byte[] sendBuffer = new byte[1024 * 4];
    private readonly byte[] receiveBuffer = new byte[1024 * 4];

    // Lifecycle
    private Task connectedTask = Task.FromException(new InvalidOperationException("Not connected."));

    // Disposables
    private readonly ClientWebSocket websocket;

    // Dependencies
    private readonly MessageIdFactory messageIdFactory;

    protected ConsoleSession(ILogger<ConsoleSession> logger)
    {
        this.logger = logger;
        this.messageIdFactory = new MessageIdFactory();
        this.websocket = new ClientWebSocket();
    }

    internal static async Task ConnectAsync(
        Uri consoleWebsocketUri, 
        string authToken,
        ILogger<ConsoleSession> logger,
        Action<ConsoleSession> onSessionConnected,
        Action<ConsoleSession, IAsyncEnumerable<GameConsoleEvent>> handleEvents, 
        Action<Exception?> onDisconnected,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        DateTimeOffset lastMessage = DateTimeOffset.UtcNow;
        if (consoleWebsocketUri.Scheme != "ws")
        {
            throw new ArgumentException("Uri must be an unsecured websocket uri.", nameof(consoleWebsocketUri));
        }

        ConsoleSession consoleSession = new ConsoleSession(logger);
        await consoleSession.websocket.ConnectAsync(consoleWebsocketUri, cancellationSource.Token);

        Task keepAliveTask = consoleSession.KeepAliveAsync(cancellationSource.Token);

        async IAsyncEnumerable<GameConsoleEvent> eventsEmitter()
        {
            IAsyncEnumerator<JsonNode> enumerator = consoleSession.ReceiveMessagesAsync(cancellationSource.Token).GetAsyncEnumerator();
            JsonNode? result = default;
            bool hasResult = true;
            while (hasResult)
            {
                try
                {
                    hasResult = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    if (hasResult)
                    {
                        result = enumerator.Current;
                    }
                    else
                    {
                        result = null;
                    }
                }
                catch (Exception ex)
                {
                    onDisconnected(ex);
                    cancellationSource.Cancel();
                }
                if (result != null)
                {
                    yield return new GameConsoleEvent(result);
                }
            }

            cancellationSource.Cancel();
            yield break;
        }

        // publish event handling
        handleEvents(consoleSession, eventsEmitter());

        if (!await consoleSession.TryAuthorizeAsync(authToken, AuthTimeout, cancellationSource.Token))
        {
            throw new InvalidOperationException("Unable to authorize the connection.");
        }

        onSessionConnected(consoleSession); // Command handler interface?  This is probably fine though.
    }

    private async IAsyncEnumerable<JsonNode> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool authMessageExpected = true;

        while (!cancellationToken.IsCancellationRequested && this.websocket.State == WebSocketState.Open)
        {
            // Rent a buffer from the array pool
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 4);

            try
            {
                WebSocketReceiveResult? result = default;
                using MemoryStream totalStream = new MemoryStream();

                do
                {
                    // Use the rented buffer for receiving data
                    try
                    {
                        result = await this.websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error receiving message from console.");
                        continue;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        yield break;
                    }

                    // Message received, reset the idle timer
                    this.MarkLastMessageTime();

                    totalStream.Write(rentedBuffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        string rawMessage = Encoding.UTF8.GetString(totalStream.ToArray());
                        totalStream.SetLength(0);
                        if (this.logger.IsEnabled(LogLevel.Trace))
                        {
                            this.logger.LogTrace("RECV: {rawMessage}", rawMessage);
                        }

                        var messageJson = JsonNode.Parse(rawMessage!) ?? throw new InvalidOperationException($"Unable to parse the message {rawMessage} as a json object.");

                        // should we have a reusable message parser or something? These could all be combined.
                        if (authMessageExpected && this.IsAuthorizationGrantedResponse(messageJson))
                        {
                            // mark us as authorized.
                            authMessageExpected = false;
                            this.authenticatedTcs.SetResult(true);
                            continue;
                        }

                        if (this.IsResponseMessage(messageJson))
                        {
                            // complete the response.
                            var id = messageJson["commandId"]!.GetValue<long>();

                            if (this.pendingRequests.TryGetValue(id, out var tcs))
                            {
                                tcs.SetResult(messageJson);
                            }
                        }


                        if (this.IsEventMessage(messageJson))
                        {
                            yield return messageJson;
                        }
                    }
                } while ((!result?.EndOfMessage ?? false) && (!cancellationToken.IsCancellationRequested));
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        yield break;
    }

    private bool IsEventMessage(JsonNode messageJson) => messageJson["eventType"] != null;

    private bool IsResponseMessage(JsonNode messageJson) => messageJson["commandId"] != null;

    private bool IsAuthorizationGrantedResponse(JsonNode messageJson)
    {
        string eventType = messageJson["eventType"]?.GetValue<string>() ?? "";

        if (eventType != "InfoLog")
        {
            return false;
        }

        string dataValue = messageJson["data"]?.GetValue<string>() ?? "";

        if (dataValue == string.Empty)
        {
            return false;
        }

        return dataValue.StartsWith("Connection Succeeded, Authenticated as:");
    }

    public Task SendCommandString(string commandString, CancellationToken cancellationToken = default)
    {
        return this.SendRawStringAsync(commandString, cancellationToken);
    }

    public async Task<CommandResult> RunCommand(string commandString, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<JsonNode> tcs = new TaskCompletionSource<JsonNode>();
        await this.sendSemaphore.WaitAsync(cancellationToken);

        var requestId = this.messageIdFactory.GetNextId();
        string commandMessageJson = JsonSerializer.Serialize(new {id = requestId, content = commandString});

        Task sendTask = this.SendRawStringAsync(commandMessageJson, cancellationToken)
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

        if (!this.pendingRequests.TryAdd(requestId, tcs))
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
                        return CommandResult.Timeout();
                    }
                    else
                    {
                        return CommandResult.Error(task.Exception?.ToString() ?? "Unknown Error Occurred.");
                    }
                }
                else if (task.IsCanceled)
                {
                    return CommandResult.Cancelled();
                }

                return CommandResult.Completed(task.Result);
            });

        try
        {
            await sendTask; // just to close the loop.
            var response = await result.ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, $"Failed to send request {commandString}");
            throw;
        }
        finally
        {
            this.pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task<bool> TryAuthorizeAsync(string authToken, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // what does failure look like
        var sendTask = this.SendRawStringAsync(authToken, cancellationToken);

        var authenticatedTask = this.authenticatedTcs.Task.WaitAsync(timeout)
           .ContinueWith(task =>
           {
               return task.IsCompletedSuccessfully;
           })
           .ConfigureAwait(false);

        await sendTask.ConfigureAwait(false);
        return await authenticatedTask;
    }

    private async Task SendRawStringAsync(string messageJson, CancellationToken cancellationToken)
    {
        if (!this.Ready)
        {
            throw new InvalidOperationException("The client is not ready.  Make sure it is connected and not disposed.");
        }

        if (this.logger.IsEnabled(LogLevel.Trace))
        {
            this.logger.LogTrace($"SEND: {messageJson}");
        }

        byte[] messageBytes = Encoding.Default.GetBytes(messageJson);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(messageBytes.Length);
        try
        {
            Buffer.BlockCopy(messageBytes, 0, buffer, 0, messageBytes.Length);
            var arraySegment = new ArraySegment<byte>(buffer, 0, messageBytes.Length);

            await this.websocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            this.MarkLastMessageTime();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    ////////////////////
    // Idle ping timeout
    ////////////////////
    private async Task KeepAliveAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(IdleConnectionCheckPeriod, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                this.logger.LogTrace(ex, "Keepalive operation cancelled.");
                continue;
            }
            
            this.logger.LogTrace("Checking for idle timeout");

            if (DateTimeOffset.UtcNow - this.lastMessage > IdleConnectionTimeout)
            {
                // We haven't received a message in a while, we should ping.
                try
                {
                    this.logger.LogTrace("Idle keepalive, pinging.");
                    await this.SendRawStringAsync("ping", cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Idle keepalive failed. {ex}");
                }
            }
        }
    }

    private void MarkLastMessageTime()
    {
        this.lastMessage = DateTimeOffset.UtcNow;
    }

    ////////////////////
    // Readiness
    ////////////////////
    public bool Ready => this.websocket?.State == WebSocketState.Open;
}
