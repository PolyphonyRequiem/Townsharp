namespace Townsharp.Infrastructure.Websockets;

internal class MessageIdFactory
{
    int messageId = -1;

    internal int MessagesSent => messageId;

    internal int GetNextId()
    {
        return Interlocked.Increment(ref messageId);
    }
}