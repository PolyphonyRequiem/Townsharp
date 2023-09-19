using System.Buffers;
using System.Net.WebSockets;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Websockets;

internal abstract class WebsocketMessageClient
{
    // Constants
    private static readonly TimeSpan IdleConnectionTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan IdleConnectionCheckPeriod = TimeSpan.FromMinutes(2);

    // Logging
    protected readonly ILogger logger;

    // State
    private DateTimeOffset lastMessage = DateTimeOffset.UtcNow;

    // Buffers
    private readonly byte[] sendBuffer = new byte[1024 * 4];
    private readonly byte[] receiveBuffer = new byte[1024 * 4];

    // Lifecycle
    private Task? receiverTask;
    private Task? idleKeepaliveTask;
    private WebsocketMessageClientState state;

    protected WebsocketMessageClientState State => this.state;
    
    // Disposables
    private readonly ClientWebSocket websocket;

    // Events
    internal event EventHandler? Disconnected;

    protected WebsocketMessageClient(ILogger logger)
    {
        this.state = WebsocketMessageClientState.Created;
        this.websocket = new ClientWebSocket();
        this.logger = logger;
    }

    public async Task ConnectAsync()
    {
        if (this.state != WebsocketMessageClientState.Created)
        {
            throw new InvalidOperationException("Cannot connect client unless it was just created.");
        }

        this.state = WebsocketMessageClientState.Connecting;
        
        await this.ConnectClientWebSocket(this.websocket);

        if (this.websocket.State != WebSocketState.Open)
        {
            await this.DisconnectAsync();
            this.state = WebsocketMessageClientState.Disposed;
        }

        this.state = WebsocketMessageClientState.Connected;

        this.receiverTask = this.ReceiveMessagesAsync();
        //this.idleKeepaliveTask = this.KeepAliveAsync();
    }

    protected abstract Task ConnectClientWebSocket(ClientWebSocket clientWebSocket);

    protected abstract void HandleMessage(string message);

    protected async Task SendMessageAsync(string message)
    {
        if (!this.Ready)
        {
            throw new InvalidOperationException("The client is not ready.  Make sure it is connected and not disposed.");
        }

        if (this.logger.IsEnabled(LogLevel.Trace))
        {
            this.logger.LogTrace($"SEND: {message}");
        }

        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(messageBytes.Length);
        try
        {
            Buffer.BlockCopy(messageBytes, 0, buffer, 0, messageBytes.Length);
            var arraySegment = new ArraySegment<byte>(buffer, 0, messageBytes.Length);

            await this.websocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            this.MarkLastMessageTime();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        while (this.Ready)
        {
            // Rent a buffer from the array pool
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 4);
            using MemoryStream ms = new MemoryStream();

            try
            {
                WebSocketReceiveResult result;
                using MemoryStream totalStream = new MemoryStream();

                do
                {
                    try
                    {
                        // Use the rented buffer for receiving data
                        result = await this.websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), CancellationToken.None);

                        // Message received, reset the idle timer
                        this.MarkLastMessageTime();
                    }
                    catch (WebSocketException ex)
                    {
                        this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(ReceiveMessagesAsync)}.  {ex}");
                        await this.DisconnectAsync();
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        this.logger.LogWarning($"{nameof(WebsocketMessageClient)} operation has been cancelled in {nameof(ReceiveMessagesAsync)}.");
                        await this.DisconnectAsync();
                        break;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(ReceiveMessagesAsync)}.  {ex}");
                        await this.DisconnectAsync();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await this.DisconnectAsync();
                        break;
                    }

                    totalStream.Write(rentedBuffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        string rawMessage = Encoding.UTF8.GetString(totalStream.ToArray());
                        totalStream.SetLength(0);

                        if (this.logger.IsEnabled(LogLevel.Trace))
                        {
                            this.logger.LogTrace($"RECV: {rawMessage}");
                        }

                        this.HandleMessage(rawMessage);
                    }
                } while (!result.EndOfMessage && this.state == WebsocketMessageClientState.Connected);
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private async Task KeepAliveAsync()
    {
        while (this.Ready)
        {
            await Task.Delay(IdleConnectionCheckPeriod).ContinueWith(async task =>
            {
                if (!this.Ready)
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
                        await this.SendMessageAsync("ping").ConfigureAwait(false);
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

    internal bool Ready => 
        this.state == WebsocketMessageClientState.Connected &&
        this.websocket?.State == WebSocketState.Open;

    public async Task DisconnectAsync()
    {
        try
        {
            if (this.state == WebsocketMessageClientState.Created)
            {
                this.state = WebsocketMessageClientState.Disposed;
                return;
            }

            if (this.state == WebsocketMessageClientState.Disposed)
            {
                this.state = WebsocketMessageClientState.Disposed;
                return;
            }

            if (this.state == WebsocketMessageClientState.Connecting)
            {
                this.websocket.Dispose();
                this.Disconnected?.Invoke(this, EventArgs.Empty);
                this.state = WebsocketMessageClientState.Disposed;
            }

            if (this.state == WebsocketMessageClientState.Connected)
            {
                if (this.websocket.State == WebSocketState.Open)
                {
                    await this.websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by Townsharp client.", CancellationToken.None);
                }

                if (this.receiverTask != null)
                {
                    await this.receiverTask.ConfigureAwait(false);
                }

                if (this.idleKeepaliveTask != null)
                {
                    await this.idleKeepaliveTask.ConfigureAwait(false);
                }

                this.websocket.Dispose();
                this.Disconnected?.Invoke(this, EventArgs.Empty);
                this.state = WebsocketMessageClientState.Disposed;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(DisconnectAsync)}.  {ex}");
        }
    }
}
