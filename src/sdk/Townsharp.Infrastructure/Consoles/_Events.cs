namespace Townsharp.Infrastructure.Consoles;

public enum ConsoleEventType
{
    PlayerMovedChunkEvent,
    PlayerJoinedEvent,
    PlayerLeftEvent
}

public abstract record ConsoleEvent(ConsoleEventType ConsoleEventType);

public record PlayerMovedChunkEvent(PlayerInfo player, string oldChunk, string newChunk) : ConsoleEvent(ConsoleEventType.PlayerMovedChunkEvent)
{
    public override string ToString()
    {
        return $"PlayerMovedChunk - {player} - {oldChunk} -> {newChunk}";
    }
}

public record PlayerInfo(int id, string username)
{
    public override string ToString()
    {
        return $"{username} ({id})";
    }
}

public record PlayerJoinedEvent(PlayerInfo user, string mode, float[] position) : ConsoleEvent(ConsoleEventType.PlayerJoinedEvent)
{
    public override string ToString()
    {
        return $"PlayerJoined - {user} - {mode} -> [{position[0]}, {position[1]}, {position[2]}]";
    }
}

public record PlayerLeftEvent(PlayerInfo user, string mode, float[] position) : ConsoleEvent(ConsoleEventType.PlayerLeftEvent)
{
    public override string ToString()
    {
        return $"PlayerLeft - {user} - {mode} -> [{position[0]}, {position[1]}, {position[2]}]";
    }
}