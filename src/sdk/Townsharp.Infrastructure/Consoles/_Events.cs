using System.Numerics;

using Townsharp.Infrastructure.Models;

namespace Townsharp.Infrastructure.Consoles;

public enum ConsoleEventType
{
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
    SocialTabletPlayerReported
}


public abstract record ConsoleEvent(ConsoleEventType ConsoleEventType);

public record PlayerStateChangedEvent(UserInfo player, PlayerStateType state, bool isEnter) : ConsoleEvent(ConsoleEventType.PlayerStateChanged)
{
    public override string ToString()
    {
        return $"PlayerStateChanged - {player} - state: {state} - isEnter: {isEnter}";
    }
}

public record PlayerJoinedEvent(UserInfo user, string mode, Vector3 position) : ConsoleEvent(ConsoleEventType.PlayerJoined)
{
    public override string ToString()
    {
        return $"PlayerJoined - {user} - {mode} -> {position}";
    }
}

public record PlayerLeftEvent(UserInfo user, string mode, Vector3 position) : ConsoleEvent(ConsoleEventType.PlayerLeft)
{
    public override string ToString()
    {
        return $"PlayerLeft - {user} - {mode} -> {position}";
    }
}

public record PlayerKilledEvent(UserInfo killedPlayer, string? usedTool, string? toolWielder, UserInfo? killerPlayer, DamageSource source) : ConsoleEvent(ConsoleEventType.PlayerKilled)
{
    public override string ToString()
    {
        string killerPlayerString = killedPlayer?.ToString() ?? "No Killer Player";
        return $"PlayerKilled - {killedPlayer} -> {usedTool ?? "No Tool"} - {toolWielder ?? "No Tool Wielder"} - {killerPlayerString} - Source: {source}";
    }
}

public record PopulationModifiedEvent(string populationName, string chunkIdentifier, int currentPopulation, int maxPopulation, PopulationAction action) : ConsoleEvent(ConsoleEventType.PopulationModified)
{
    public override string ToString()
    {
        return $"PopulationModified - {populationName} - {chunkIdentifier} - {currentPopulation}/{maxPopulation} - {action}";
    }
}

public record TradeDeckUsedEvent(string itemName, uint itemHash, float price, int quantity, int buyer, int seller) : ConsoleEvent(ConsoleEventType.TradeDeckUsed)
{
    public override string ToString()
    {
        return $"TradeDeckUsed - {itemName} - {itemHash} - {price} - {quantity} - {buyer} - {seller}";
    }
}

public record PlayerMovedChunkEvent(UserInfo player, string oldChunk, string newChunk) : ConsoleEvent(ConsoleEventType.PlayerMovedChunk)
{
    public override string ToString()
    {
        return $"PlayerMovedChunk - {player} - {oldChunk} -> {newChunk}";
    }
}

public record ObjectKilledEvent(uint identifier, uint prefab, string name) : ConsoleEvent(ConsoleEventType.ObjectKilled)
{
    public override string ToString()
    {
        return $"ObjectKilled - {identifier} - {prefab} - {name}";
    }
}

public record TrialStartedEvent(UserInfo[] users, string trial) : ConsoleEvent(ConsoleEventType.TrialStarted)
{
    public override string ToString()
    {
        string usersString = string.Join(", ", users.Select(u => u.ToString()));
        return $"TrialStarted - {usersString} - {trial}";
    }
}

public record TrialFinishedEvent(UserInfo[] users, string trial) : ConsoleEvent(ConsoleEventType.TrialFinished)
{
    public override string ToString()
    {
        string usersString = string.Join(", ", users.Select(u => u.ToString()));
        return $"TrialFinished - {usersString} - {trial}";
    }
}

public record InventoryChangedEvent(UserInfo User, string ItemName, int Quantity, Unit? ItemHash, string? Material, string SaveString, InventoryChangeType ChangeType, InventoryType InventoryType, UserInfo? DestinationUser) : ConsoleEvent(ConsoleEventType.InventoryChanged)
{
    public override string ToString()
    {
        return $"InventoryChanged - {User} - {ItemName} - {Quantity} - {ItemHash} - {Material} - {SaveString} - {ChangeType} - {InventoryType} - {DestinationUser}";
    }
}

public record AtmBalanceChangedEvent(UserInfo User, int Change, int CurrentBalance) : ConsoleEvent(ConsoleEventType.AtmBalanceChanged)
{
    public override string ToString()
    {
        return $"AtmBalanceChanged - {User} - {Change} - {CurrentBalance}";
    }
}

public record ServerSettingsChangedEvent() : ConsoleEvent(ConsoleEventType.ServerSettingsChanged)
{
    public ServerSettingsAge ServerSettingsAge;

    public bool IsAutoSaving = true;

    public float MinutesBetweenAutoSaving = 5f;

    public bool DropAllOnDeath;

    public bool HandoverRequireConfirmation = true;

    public float RespawnTimeSeconds = 4f;

    public float TimeSpeedMultiplier = 1f;

    public bool IsAdvancingTimeWhenOffline = true;

    public bool IsPVPEnabled;

    public float PVPMultiplier = 0.1f;

    public float PVPCrippleMultiplier = 0.3f;

    public float HungerTick = 0.0005f;

    public float HealthRegenMultiplier = 1f;

    public float DamageX = 1f;

    public float PlayerDamageTakenMultiplier = 1f;

    public bool HungerDealsDamage = true;

    public bool AvoidSpawnCollectedPages = true;

    public float XPX = 1f;

    public float ForgingXPX = 1f;

    public float WoodcuttingXPX = 1f;

    public float MiningXPX = 1f;

    public float ChiselingXPX = 1f;

    public float CookingXPX = 1f;

    public float MeleeXPX = 1f;

    public float RangedXPX = 1f;

    public float UnspentExperienceLostOnDeath;

    public bool CanOnlyBuySkillsAfterPracticing = true;

    public int PlayerLimit = -1;

    public int EmptyServerShutdownTimeInSeconds = 300;

    public int FPSForMaximumFixedFrameCount = 20;

    public int FPSForMinimumFixedFrameCount = 5;

    public int MaximumPhysicsFixedFramesPerSecond = 45;

    public int MinimumPhysicsFixedFramesPerSecond = 15;

    public bool IsUpdatingPhysicsFixedFrameCount = true;

    public PlayerLandmarkMode PlayerLandmarkMode;

    public float FixedDaytime = -1f;

    public bool IsResetingHungerOnDeath;

    public PlayerSmoothingLevel PlayerSmoothingLevel;

    public InitialInventory InitialPlayerInventory = InitialInventory.Default;

    public float SpawningRateMultiplier = 1f;

    public float DecayMultiplier = 1f;

    public float CommunityStorageMultiplier = 1f;

    public bool IsCustomizationEnabled = true;

    public bool IsSaving = true;

    public bool HasAFKTimer = true;

    public bool HasInGameSettings;

    public bool WipesWhenEmpty;

    public bool HasRunRepairBoxFix;
}

public record CommandExecutedEvent(string Command, bool WasSuccessful, string Message) : ConsoleEvent(ConsoleEventType.CommandExecuted)
{
    public override string ToString()
    {
        return $"CommandExecuted - {Command} - WasSuccessful: {WasSuccessful} - string {Message}";
    }
}

public record SocialTabletPlayerBannedEvent(UserInfo BannedBy, UserInfo BannedPlayer, bool IsBan) : ConsoleEvent(ConsoleEventType.SocialTabletPlayerBanned)
{
    public override string ToString()
    {
        return $"SocialTabletPlayerBanned - BannedBy: {BannedBy} - BannedPlayer: {BannedPlayer} - IsBan: {IsBan}";
    }
}

public record SocialTabletPlayerReportedEvent(UserInfo ReportedBy, UserInfo ReportedPlayer, string Reason) : ConsoleEvent(ConsoleEventType.SocialTabletPlayerReported)
{
    public override string ToString()
    {
        return $"SocialTabletPlayerReported - ReportedBy: {ReportedBy} - ReportedPlayer: {ReportedPlayer} - Reason: {Reason}";
    }
}

public enum PlayerStateType
{
    None,
    FirstJoin,
    Playing,
    Combat,
    Customization,
    Equipment,
    Dead,
    Spirit,
    Downed
}

public enum DamageSource
{
    FallDamage,
    Impact,
    Command,
    AreaOfEffect,
    Poison,
    Hunger,
    Unknown,
    Fire
}

public enum PopulationAction
{
    Initialize,
    Update,
    Spawned,
    Lost
}

public enum InventoryChangeType
{
    Pickup,
    Drop,
    Dock,
    Undock
}

public enum InventoryType
{
    World,
    Player
}

public enum ServerSettingsAge
{
    None,
    New,
    Old
}

public enum PlayerLandmarkMode
{
    ShowAll,
    FriendsOnly,
    Hidden
}

public enum PlayerSmoothingLevel
{
    Default,
    Low
}

public enum InitialInventory
{
    None,
    DeckedOut,
    Default
}