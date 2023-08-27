namespace Townsharp.Domain;

public class GameServer
{
    private readonly GameServerId id;

    public GameServerId Id => id;
}

public readonly record struct GameServerId
{
    private readonly ulong value;

    public GameServerId(ulong value)
    {
        this.value = value;
    }

    public static implicit operator ulong(GameServerId id) => id.value;
    public static explicit operator GameServerId(ulong value) => new GameServerId(value);
}

