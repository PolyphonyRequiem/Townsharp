using System.Numerics;

namespace Townsharp.Consoles;

public enum ConsoleEventType
{
    TraceLog,
    DebugLog,
    InfoLog,
    WarnLog,
    ErrorLog,
    FatalLog,
    PlayerStateChanged,
    PlayerJoined,
    PlayerLeft,
    PlayerKilled,
    PopulationModified,
    TradeDeckUsed,
    PlayerMovedChunk,
    ObjectKilled,
    TrialStarted,
    TrialFinished,
    InventoryChanged,
    AtmBalanceChanged,
    ServerSettingsChanged,
    CommandExecuted,
    SocialTabletPlayerBanned,
    SocialTabletPlayerReported,
    ProfilingData
}

public abstract record class ConsoleEvent
{
    internal ConsoleEvent(DateTimeOffset timestamp, ConsoleEventType eventType)
    {
        this.Timestamp = timestamp;
        this.EventType = eventType;
    }

    public DateTimeOffset Timestamp { get; init; }

    public ConsoleEventType EventType { get; init; }
}

public abstract record class PlayerJoinedEvent : ConsoleEvent
{
    protected PlayerJoinedEvent(DateTimeOffset timestamp, UserInfo user, string mode, Vector3 position)
        : base(timestamp, ConsoleEventType.PlayerJoined)
    {
        this.User = user;
        this.Mode = mode;
        this.Position = position;
    }

    public UserInfo User { get; init; }

    public string Mode { get; init; }

    public Vector3 Position { get; init; }
}
