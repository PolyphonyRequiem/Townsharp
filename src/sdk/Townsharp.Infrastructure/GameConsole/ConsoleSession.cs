using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.ServerConsole;

public class ConsoleSession : IDisposable, IAsyncDisposable
{
    // Constants
    public static int MAX_CONCURRENT_REQUESTS = 4;
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
    private Task receiverTask = Task.CompletedTask;
    private Task idleKeepaliveTask = Task.CompletedTask;

    private bool connected = false;
    private bool disposed = false;

    // Disposables
    private readonly ClientWebSocket websocket;
    private readonly CancellationTokenSource cancellationTokenSource;

    // Dependencies
    private readonly MessageIdFactory messageIdFactory;

    // Events
    public event EventHandler<GameConsoleEvent>? OnGameConsoleEvent;
    public event EventHandler? OnWebsocketFaulted;

    // NOTE TO SELF 8/8/2023 - Okay, hear me out.  The functional design is actually pretty good in concept, INTERNALLY, but I think the external scenario needs a little thinking through.  
    // The main concern I seem to face with the websockets scenario is around lifecycle management, which is why the SubscriptionConnection is such a damned mess.
    // For console, we should probably address that, but dropped connections are more normal here and should probably result in different considerations upstream for lifecycle management.
    // To this end, I think we should probably focus on the domain library design at this point, and then come back to this later.  I think the domain library design will inform the overall
    // design of the infra library going forward.
    protected ConsoleSession(ILogger<ConsoleSession> logger)
    {
        this.logger = logger;
        this.messageIdFactory = new MessageIdFactory();
        this.websocket = new ClientWebSocket();
        this.cancellationTokenSource = new CancellationTokenSource();
    }

    internal static async Task<ConsoleSession> CreateAndConnectAsync(Uri consoleWebsocketUri, string authToken, ILogger<ConsoleSession> logger)
    {
        ConsoleSession consoleSession = new ConsoleSession(logger);
        await consoleSession.ConnectAsync(consoleWebsocketUri, authToken);

        return consoleSession;
    }

    private async Task ConnectAsync(Uri consoleWebsocketUri, string authToken)
    {
        if (this.connected)
        {
            throw new InvalidOperationException("Console already connected.");
        }

        if (consoleWebsocketUri.Scheme != "ws")
        {
            throw new ArgumentException("Uri must be an unsecured websocket uri.", nameof(consoleWebsocketUri));
        }

        await this.websocket.ConnectAsync(consoleWebsocketUri, this.cancellationTokenSource.Token);

        if (this.websocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException($"Failed to connect to subscription endpoint. {this.websocket.CloseStatus ?? WebSocketCloseStatus.Empty} :: {this.websocket.CloseStatusDescription ?? string.Empty}.");
        }

        this.connected = true;
        this.receiverTask = this.ReceiveMessagesAsync();
        this.idleKeepaliveTask = this.KeepAliveAsync();
    
        if (!await this.TryAuthorizeAsync(authToken, AuthTimeout, this.cancellationTokenSource.Token))
        {
            await this.DisconnectAsync();
            throw new InvalidOperationException("Unable to authorize the connection.");
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            if (this.websocket.State == WebSocketState.Open)
            {
                await this.websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by Townsharp client.", this.cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError($"{nameof(ConsoleSession)} Error has occurred in {nameof(DisconnectAsync)}.  {ex}");
        }
        finally
        {
            if (this.connected == true)
            {
                this.OnWebsocketFaulted?.Invoke(this, EventArgs.Empty);
            }

            this.connected = false;

            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                this.cancellationTokenSource.Cancel();
            }
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        bool authMessageExpected = true;

        while (!this.cancellationTokenSource.IsCancellationRequested && this.websocket.State == WebSocketState.Open)
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
                        result = await this.websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), this.cancellationTokenSource.Token);

                        // Message received, reset the idle timer
                        this.MarkLastMessageTime();
                    }
                    catch (WebSocketException ex)
                    {
                        // stop listening, we are done.
                        this.logger.LogError($"{nameof(ConsoleSession)} Error has occurred in {nameof(ReceiveMessagesAsync)}.  {ex}");
                        await this.DisconnectAsync();
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // stop listening, we are done.
                        this.logger.LogWarning($"{nameof(ConsoleSession)} operation has been cancelled in {nameof(ReceiveMessagesAsync)}.");
                        await this.DisconnectAsync();
                        break;
                    }
                    catch (Exception ex)
                    {
                        // stop listening, we are done.
                        this.logger.LogError($"{nameof(ConsoleSession)} Error has occurred in {nameof(ReceiveMessagesAsync)}.  {ex}");
                        await this.DisconnectAsync();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // stop listening, we are done.
                        break;
                    }


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
                            this.OnGameConsoleEvent?.Invoke(this, new GameConsoleEvent(messageJson));
                        }
                    }
                } while ((!result?.EndOfMessage ?? false) && (!this.cancellationTokenSource.Token.IsCancellationRequested));
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
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
    private async Task KeepAliveAsync()
    {
        while (!this.cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(IdleConnectionCheckPeriod, this.cancellationTokenSource.Token).ConfigureAwait(false);
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
                    await this.SendRawStringAsync("ping", this.cancellationTokenSource.Token).ConfigureAwait(false);
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
    public bool Ready =>
        this.connected &&
        !this.cancellationTokenSource.IsCancellationRequested &&
        !this.disposed &&
        this.websocket?.State == WebSocketState.Open;

    ////////////////////
    // Disposal
    ////////////////////
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.websocket.Dispose();
                this.cancellationTokenSource.Dispose();
            }

            this.disposed = true;
        }
    }

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsyncCore().ConfigureAwait(false);
        this.Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await this.DisconnectAsync().ConfigureAwait(false);

        if (this.receiverTask != null)
        {
            await this.receiverTask.ConfigureAwait(false);
        }

        if (this.idleKeepaliveTask != null)
        {
            await this.idleKeepaliveTask.ConfigureAwait(false);
        }
    }
}
