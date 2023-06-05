namespace Townsharp.Infrastructure.Utilities;
public class MessageIdFactory
{
    long messageId = 0;

    public long MessagesSent => messageId;

    public long GetNextId()
    {
        return Interlocked.Increment(ref messageId);
    }
}