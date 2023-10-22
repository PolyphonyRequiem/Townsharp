using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

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

    private Channel<string> messageChannel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = true
        });

    // Lifecycle
    private WebsocketMessageClientState state;
    private CancellationTokenSource? cancellationTokenSource;
    private Task messageHandlerTask = Task.CompletedTask;

    internal WebsocketMessageClientState State => this.state;

    // Disposables
    private readonly ClientWebSocket websocket;

    // Error
    internal bool ErrorOccurred { get; private set; } = false;

    internal string? ErrorMessage { get; private set; }

    protected WebsocketMessageClient(ILogger logger)
    {
        this.state = WebsocketMessageClientState.Created;
        this.websocket = new ClientWebSocket();
        this.logger = logger;
    }

    protected IAsyncEnumerable<string> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        return this.messageChannel.Reader.ReadAllAsync(cancellationToken);
    }

    private async Task HandleMessagesAsync()
    {
        if (this.state != WebsocketMessageClientState.Connected)
        {
            throw new InvalidOperationException("Cannot handle messages unless the client is connected.");
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
                        result = await this.websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), this.cancellationTokenSource!.Token).ConfigureAwait(false);

                        // Message received, reset the idle timer
                        // this.MarkLastMessageTime();
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await this.AbortAsync().ConfigureAwait(false);
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        await this.AbortAsync($"{nameof(WebsocketMessageClient)} operation has been cancelled in {nameof(HandleMessagesAsync)}.").ConfigureAwait(false);
                        break;
                    }
                    catch (Exception ex)
                    {
                        await this.AbortAsync($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(HandleMessagesAsync)}.  {ex}").ConfigureAwait(false);
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

                        await this.messageChannel.Writer.WriteAsync(rawMessage, this.cancellationTokenSource!.Token).ConfigureAwait(false);
                    }
                } while (!result.EndOfMessage && this.state == WebsocketMessageClientState.Connected && !this.cancellationTokenSource!.Token.IsCancellationRequested);
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private void SetError(string? errorMessage = default)
    {
        this.ErrorOccurred = true;
        this.ErrorMessage = errorMessage;
    }

    // might be able to merge with Receive Messages, and get rid of the bool here.
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.state != WebsocketMessageClientState.Created)
            {
                throw new InvalidOperationException("Cannot connect client unless it was just created.");
            }

            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            this.state = WebsocketMessageClientState.Connecting;

            await this.ConfigureClientWebsocket(this.websocket).ConfigureAwait(false);

            if (this.websocket.State != WebSocketState.Open)
            {
                await this.AbortAsync().ConfigureAwait(false);
                this.state = WebsocketMessageClientState.Disposed;
                return false;
            }

            this.state = WebsocketMessageClientState.Connected;
            this.messageHandlerTask = this.HandleMessagesAsync();
            bool success = await this.OnConnectedAsync();
            if (!success)
            {
                await this.AbortAsync().ConfigureAwait(false);
                this.state = WebsocketMessageClientState.Disposed;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(ConnectAsync)}.  {ex}");
            return false;
        }
    }

    protected abstract Task ConfigureClientWebsocket(ClientWebSocket websocket);

    protected abstract Task<bool> OnConnectedAsync();

    protected abstract Task OnDisconnectedAsync();

    internal async Task SendMessageAsync(string message)
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

            await this.websocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, this.cancellationTokenSource?.Token ?? CancellationToken.None).ConfigureAwait(false);
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
        this.websocket?.State == WebSocketState.Open &&
        !(this.cancellationTokenSource?.IsCancellationRequested ?? false);

    private async Task AbortAsync(string? errorMessage = default)
    {
        try
        {
            this.logger.LogDebug($"Attempting to abort the websocket with error message '{errorMessage ?? ""}'");
            this.logger.LogDebug("Triggering internal cancellation token source.");
            this.cancellationTokenSource?.Cancel();

            if (this.state == WebsocketMessageClientState.Created)
            {
                // We never made it around to creating the underlying websocket.
                this.logger.LogDebug("We never made it around to creating the underlying websocket.");
                this.state = WebsocketMessageClientState.Disposed;
                return;
            }

            if (this.state == WebsocketMessageClientState.Disposed)
            {
                // We are already disposed.
                this.logger.LogDebug("We are already disposed.");
                this.state = WebsocketMessageClientState.Disposed;
                return;
            }

            if (this.state == WebsocketMessageClientState.Connecting)
            {
                // We are still connecting, we can just dispose the websocket.
                this.logger.LogDebug("We are still connecting, we can just dispose the websocket.");
                this.SetError(errorMessage);
                this.websocket.Dispose();
                this.state = WebsocketMessageClientState.Disposed;
            }

            if (this.state == WebsocketMessageClientState.Connected)
            {
                // We are connected, we need to close the websocket.
                this.logger.LogDebug("We are connected, we need to close the websocket.");
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
            this.logger.LogError($"{nameof(WebsocketMessageClient)} Error has occurred in {nameof(AbortAsync)}.  {ex}");
        }
        finally
        {
            if (!this.messageChannel.Reader.Completion.IsCompleted)
            {
                this.messageChannel.Writer.TryComplete();
            }

            this.messageHandlerTask = Task.CompletedTask;
        }
    }
}
