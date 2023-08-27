namespace Townsharp;

public class ServerGroup
{
    private readonly ServerGroupId id;

    public ServerGroupId Id => id;
}

public readonly record struct ServerGroupId
{
    private readonly ulong value;

    public ServerGroupId(ulong value)
    {
        this.value = value;
    }

    public static implicit operator long(ServerGroupId id) => (long) id.value;
    public static implicit operator ulong(ServerGroupId id) => id.value;
    public static implicit operator ServerGroupId(ulong value) => new ServerGroupId(value);

    public override string ToString()
    {
        return this.value.ToString();
    }
}
