namespace Townsharp.Infrastructure.Models;


public record JoinedGroupInfo(
    GroupInfoDetailed group,
    GroupMemberInfo member);

public record GroupInfoDetailed(
    GroupServerInfo[]? servers,
    int? allowed_servers_count,
    GroupRoleInfo[]? roles,
    int id,
    string? name,
    string? description,
    int member_count,
    DateTime created_at,
    string type,
    string[]? tags);

public record GroupServerInfo(
    int id,
    string name,
    int scene_index,
    string status);

public record GroupRoleInfo(
    int role_id,
    string name,
    string[] permissions,
    string[] allowed_commands);

public record GroupMemberInfo(
    int group_id,
    int user_id,
    string username,
    bool bot,
    int icon,
    string? permissions,
    int role_id,
    DateTime created_at,
    string type);