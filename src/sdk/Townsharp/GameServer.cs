namespace Townsharp;

public class GameServer
{
    private readonly GameServerId id;
    private readonly ServerGroupId groupId;

    internal GameServer(GameServerId id, ServerGroupId groupId)
    {
        this.id = id;
        this.groupId = groupId;
    }

    public GameServerId Id => id;

    public ServerGroupId GroupId => groupId;
}

public readonly record struct GameServerId
{
    private readonly ulong value;

    public GameServerId(ulong value)
    {
        this.value = value;
    }

    public static implicit operator ulong(GameServerId id) => id.value;
    public static implicit operator long(GameServerId id) => (long)id.value;
    public static implicit operator GameServerId(ulong value) => new GameServerId(value);

    public override string ToString()
    {
        return value.ToString();
    }
}

