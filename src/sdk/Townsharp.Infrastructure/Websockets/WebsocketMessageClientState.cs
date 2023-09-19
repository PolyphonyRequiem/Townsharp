namespace Townsharp.Infrastructure.Websockets;

internal enum WebsocketMessageClientState
{
    Created,
    Connecting,
    Connected,
    Disposed
}
