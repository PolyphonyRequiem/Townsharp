using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
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
    private WebsocketMessageClientState state;

    protected WebsocketMessageClientState State => this.state;
    
    // Disposables
    private readonly ClientWebSocket websocket;

    // Error
    public bool ErrorOccurred { get; private set; } = false;

    public string? ErrorMessage { get; private set; }

    protected WebsocketMessageClient(ILogger logger)
    {
        this.state = WebsocketMessageClientState.Created;
        this.websocket = new ClientWebSocket();
        this.logger = logger;
    }

    protected async IAsyncEnumerable<string> ListenForMessagesAsync([EnumeratorCancellation]CancellationToken cancellationToken)
    {
        if (this.state != WebsocketMessageClientState.Connected)
        {
            throw new InvalidOperationException("Cannot listen for messages unless the client is connected.");
        }

        //this.idleKeepaliveTask = this.KeepAliveAsync();

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
                        result = await this.websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), cancellationToken);

                        // Message received, reset the idle timer
                        // this.MarkLastMessageTime();
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await this.DisconnectAsync();
                            yield break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        await this.DisconnectAsync($"{nameof(WebsocketMessageClient)} operation has been cancelled in {nameof(ListenForMessagesAsync)}.");
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        await this.DisconnectAsync($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(ListenForMessagesAsync)}.  {ex}");
                        yield break;
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

                        yield return rawMessage;
                    }
                } while (!result.EndOfMessage && this.state == WebsocketMessageClientState.Connected && !cancellationToken.IsCancellationRequested);
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        yield break;
    }

    private void SetError(string? errorMessage = default)
    {
        this.ErrorOccurred = true;
        this.ErrorMessage = errorMessage;
    }

    public async Task<bool> ConnectAsync()
    {
        try
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
                return false;
            }

            this.state = WebsocketMessageClientState.Connected;
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(ConnectAsync)}.  {ex}");
            return false;
        }       
    }

    protected abstract Task ConnectClientWebSocket(ClientWebSocket clientWebSocket);

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

    private async Task DisconnectAsync(string? errorMessage = default)
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
                this.SetError(errorMessage);
                this.websocket.Dispose();
                this.state = WebsocketMessageClientState.Disposed;
            }

            if (this.state == WebsocketMessageClientState.Connected)
            {
                if (this.websocket.State == WebSocketState.Open)
                {
                    await this.websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by Townsharp client.", CancellationToken.None);
                }

                this.websocket.Dispose();
                this.state = WebsocketMessageClientState.Disposed;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(DisconnectAsync)}.  {ex}");
        }
    }
}
