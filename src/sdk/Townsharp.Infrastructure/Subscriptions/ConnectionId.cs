namespace Townsharp.Infrastructure.Subscriptions;

internal struct ConnectionId
{
    private readonly Guid id;

    public ConnectionId()
    {
        id = Guid.NewGuid();
    }

    public override string ToString() => id.ToString();

    public static implicit operator string(ConnectionId connectionId) => connectionId.id.ToString();
}