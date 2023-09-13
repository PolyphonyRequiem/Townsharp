namespace Townsharp.Infrastructure.WebApi.Models;

public record ServerInfo(
    ServerId Id,
    string Name,
    UserInfo[] OnlinePlayers,
    GameServerStatus ServerStatus,
    GameServerStatus FinalStatus,
    string Region,
    DateTime? LastOnline,
    string Description,
    double Playability,
    string Version,
    GroupId? GroupId,
    ServerOwnerType OwnerType,
    UserId OwnerId,
    ServerType ServerType,
    TimeSpan UpTime,
    ServerJoinType JoinType,
    int PlayerCount,
    DateTime CreatedAt,
    bool IsOnline
);
