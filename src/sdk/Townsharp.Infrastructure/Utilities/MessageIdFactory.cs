namespace Townsharp.Infrastructure.Utilities;
public class MessageIdFactory
{
    int messageId = -1;

    public int MessagesSent => messageId;

    public int GetNextId()
    {
        return Interlocked.Increment(ref messageId);
    }
}