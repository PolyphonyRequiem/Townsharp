using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Subscriptions.Models;
using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionClient : IDisposable, IAsyncDisposable
{
    // Constants
    public static int MAX_CONCURRENT_REQUESTS = 20;
    private static readonly Uri SubscriptionWebsocketUri = new Uri("wss://websocket.townshiptale.com");
    private static readonly TimeSpan IdleConnectionTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan IdleConnectionCheckPeriod = TimeSpan.FromMinutes(2);

    // State
    private DateTimeOffset lastMessage = DateTimeOffset.UtcNow;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<EventMessage>> pendingRequests = new();
    private readonly SemaphoreSlim sendSemaphore = new SemaphoreSlim(MAX_CONCURRENT_REQUESTS);

    // Buffers
    private readonly byte[] sendBuffer = new byte[1024 * 4];
    private readonly byte[] receiveBuffer = new byte[1024 * 4];

    // Lifecycle
    private Task? receiverTask;
    private Task? idleKeepaliveTask;

    private bool connected = false;
    private bool disposed = false;

    // Disposables
    private readonly ClientWebSocket websocket;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly BotTokenProvider botTokenProvider;

    // Dependencies
    private readonly ILogger<SubscriptionClient> logger;
    private readonly MessageIdFactory messageIdFactory;

    // Events
    public event EventHandler<SubscriptionEvent>? OnSubscriptionEvent;

    protected SubscriptionClient(BotTokenProvider botTokenProvider, ILogger<SubscriptionClient> logger)
    {
        this.botTokenProvider = botTokenProvider;
        this.logger = logger;
        this.messageIdFactory = new MessageIdFactory();
        this.websocket = new ClientWebSocket();       
        this.cancellationTokenSource = new CancellationTokenSource();
    }

    public static async Task<SubscriptionClient> CreateAndConnectAsync(BotTokenProvider botTokenProvider, ILogger<SubscriptionClient> logger)
    {
        var client = new SubscriptionClient(botTokenProvider, logger);
        await client.ConnectAsync();

        return client;
    }

    ////////////////////
    // Lifecycle
    ////////////////////
    protected async Task ConnectAsync()
    {
        if (this.connected)
        {
            throw new InvalidOperationException("Client already connected.");
        }
        else
        {
            this.websocket.Options.SetRequestHeader("Authorization", $"Bearer {await this.botTokenProvider.GetTokenAsync()}");
            await this.websocket.ConnectAsync(SubscriptionWebsocketUri, CancellationToken.None);
            this.connected = true;
            this.receiverTask = ReceiveEventMessagesAsync();
            this.idleKeepaliveTask = KeepAliveAsync();
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
            this.logger.LogError($"{nameof(SubscriptionClient)} Error has occurred in {nameof(DisconnectAsync)}.  {ex}");
        }
        finally
        {
            this.connected = false;
            this.cancellationTokenSource.Cancel();
        }
    }

    ////////////////////
    // Receiver
    ////////////////////
    private async Task ReceiveEventMessagesAsync()
    {
        while (this.Ready)
        {
            // Rent a buffer from the array pool
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 4);

            try
            {
                WebSocketReceiveResult result;

                do
                {
                    try
                    {
                        // Use the rented buffer for receiving data
                        result = await websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), this.cancellationTokenSource.Token);

                        // Message received, reset the idle timer
                        MarkLastMessageTime();
                    }
                    catch (WebSocketException ex)
                    {
                        // stop listening, we are done.
                        logger.LogError($"{nameof(SubscriptionClient)} Error has occurred in {nameof(ReceiveEventMessagesAsync)}.  {ex}");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // stop listening, we are done.
                        logger.LogWarning($"{nameof(SubscriptionClient)} operation has been cancelled in {nameof(ReceiveEventMessagesAsync)}.");
                        break;
                    }
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // stop listening, we are done.
                        break;
                    }

                    string rawMessage = Encoding.UTF8.GetString(rentedBuffer, 0, result.Count);

                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace($"RECV: {rawMessage}");
                    }

                    // We have the raw message!
                    this.HandleRawMessage(rawMessage);
                } while (!result.EndOfMessage && !this.cancellationTokenSource.Token.IsCancellationRequested);
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private void HandleRawMessage(string rawMessage)
    {
        // Parse message to see what we have.
        // If it's a Response message, we need to find the request and complete it.
        // If it's an Event message, we need to raise an event.
        EventMessage? eventMessage = JsonSerializer.Deserialize<EventMessage>(rawMessage);

        if (eventMessage == null)
        {
            // might be an infrastructure issue.
            InfrastructureError? infrastructureError = JsonSerializer.Deserialize<InfrastructureError>(rawMessage);

            if (infrastructureError == null)
            {
                this.logger.LogError($"Unknown message format {rawMessage}");
            }
            else
            {
                this.logger.LogError($"Infrastructure Error: {infrastructureError}");
            }
        }
        else
        {
            // If it's a response
            if (eventMessage.@event.Equals("response", StringComparison.InvariantCultureIgnoreCase))
            {               
                // complete the request
                if (this.pendingRequests.TryGetValue(eventMessage.id, out var tcs))
                {
                    tcs.SetResult(eventMessage);
                }
            }
            else // If it's an event, we raise the event.
            {
                this.OnSubscriptionEvent?.Invoke(this, SubscriptionEvent.Create(eventMessage));               
            }
        }
    }

    ////////////////////
    /// Requests
    ////////////////////
    public async Task<Response> SubscribeAsync(string eventId, long key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var id = this.messageIdFactory.GetNextId();
        RequestMessage request = RequestMessage.CreateSubscriptionRequestMessage(id, await this.botTokenProvider.GetTokenAsync(cancellationToken), eventId, key);
        return await this.SendRequestAsync(request, timeout, cancellationToken);
    }

    public async Task<Response> UnsubscribeAsync(string eventId, long key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var id = this.messageIdFactory.GetNextId();
        RequestMessage request = RequestMessage.CreateUnsubscriptionRequestMessage(id,  await this.botTokenProvider.GetTokenAsync(cancellationToken), eventId, key);
        return await this.SendRequestAsync(request, timeout, cancellationToken);
    }

    public async Task<Response> BatchSubscribeAsync(string eventId, long[] keys, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var id = this.messageIdFactory.GetNextId();
        RequestMessage request = RequestMessage.CreateBatchSubscriptionRequestMessage(id, await this.botTokenProvider.GetTokenAsync(cancellationToken), eventId, keys);
        return await this.SendRequestAsync(request, timeout, cancellationToken);
    }

    public async Task<Response> GetMigrationTokenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var id = this.messageIdFactory.GetNextId();
        RequestMessage request = RequestMessage.CreateGetMigrationTokenRequestMessage(id, await this.botTokenProvider.GetTokenAsync(cancellationToken));
        return await this.SendRequestAsync(request, timeout, cancellationToken);
    }

    public async Task<Response> SendMigrationTokenAsync(string migrationToken, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var id = this.messageIdFactory.GetNextId();
        RequestMessage request = RequestMessage.CreateSendMigrationTokenRequestMessage(id, await this.botTokenProvider.GetTokenAsync(cancellationToken), migrationToken);
        return await this.SendRequestAsync(request, timeout, cancellationToken);
    }

    ////////////////////
    // Sending
    ////////////////////
    private async Task<Response> SendRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        TaskCompletionSource<EventMessage> tcs = new TaskCompletionSource<EventMessage>();
        await this.sendSemaphore.WaitAsync(cancellationToken);

        string messageJson = JsonSerializer.Serialize(request);

        Task sendTask = this.SendRawStringAsync(messageJson, cancellationToken)
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

        if (!this.pendingRequests.TryAdd(request.id, tcs))
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
                        return Response.Timeout();
                    }
                    else
                    {
                        return Response.Error(task.Exception?.ToString() ?? "Unknown Error Occurred.");
                    }
                }
                else if (task.IsCanceled)
                {
                    return Response.Cancelled();
                }

                return Response.Completed(task.Result);
            });

        try
        {
            await sendTask; // just to close the loop.
            var response = await result.ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to send request {request}");
            throw;
        }
        finally
        {
            this.pendingRequests.TryRemove(request.id, out _);
        }
    }

    private async Task SendRawStringAsync(string messageJson, CancellationToken cancellationToken)
    {
        if (!this.Ready)
        {
            throw new InvalidOperationException("The client is not ready.  Make sure it is connected and not disposed.");
        }

        //await sendSemaphore.WaitAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"SEND: {messageJson}");
        }

        byte[] messageBytes = Encoding.Default.GetBytes(messageJson);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(messageBytes.Length);
        try
        {
            Buffer.BlockCopy(messageBytes, 0, buffer, 0, messageBytes.Length);
            var arraySegment = new ArraySegment<byte>(buffer, 0, messageBytes.Length);

            await this.websocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            MarkLastMessageTime();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            //this.sendSemaphore.Release();
        }
    }

    ////////////////////
    // Idle ping timeout
    ////////////////////
    private async Task KeepAliveAsync()
    {
        while (this.Ready)
        {
            await Task.Delay(IdleConnectionCheckPeriod, this.cancellationTokenSource.Token).ContinueWith(async task =>
            {
                if (task.IsCanceled)
                {
                    return;
                }

                this.logger.LogTrace("Checking for idle timeout");

                if (DateTimeOffset.UtcNow - this.lastMessage > IdleConnectionTimeout)
                {
                    // We haven't received a message in a while, we should ping.
                    try
                    {
                        this.logger.LogTrace("Idle keepalive, pinging.");
                        await SendRawStringAsync("ping", this.cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError($"Idle keepalive failed. {ex}");
                    }
                }
            }).ConfigureAwait(false);

        }
    }

    private void MarkLastMessageTime()
    {
        this.lastMessage = DateTimeOffset.UtcNow;
    }

    ////////////////////
    // Readiness
    ////////////////////
    private bool Ready => 
        this.connected && 
        !this.cancellationTokenSource.IsCancellationRequested &&
        !this.disposed &&
        this.websocket?.State == WebSocketState.Open;

    ////////////////////
    // Disposal
    ////////////////////
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                this.websocket.Dispose();
                this.cancellationTokenSource.Dispose();
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
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