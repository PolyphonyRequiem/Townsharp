namespace Townsharp.Infrastructure.Consoles.Models;

public enum ConsoleEventType
{
    PlayerMovedChunkEvent
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