using System.Reflection;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.WebApi;

public record ConsoleAccess(
    int server_id, 
    bool allowed, 
    string? token, 
    ConsoleConnectionInfo? connection)
{
    public Uri BuildConsoleUri()
    {
        if (!this.allowed || this.token is null || connection is null)
        {
            throw new InvalidOperationException("Unable to build a console uri from this ConsoleAccess instance. Either the console is offline, or access was denied.");
        }

        return new Uri($"ws://{this.connection.address}:{this.connection.websocket_port}");
    }
}

public record ConsoleConnectionInfo (
    string address,
    int websocket_port);

public record ServerInfo(
    int id,
    string name,
    UserInfo[] online_players,
    string server_status,
    string final_status,
    int scene_index,
    int target,
    string region,
    DateTime last_online,
    string description,
    float playability,
    string version,
    int group_id,
    string owner_type,
    int owner_id,
    string type,
    string fleet,
    TimeSpan up_time,
    string join_type,
    int player_count,
    int player_limit,
    DateTime created_at,
    bool is_online,
    int transport_system);

public record JoinedGroupInfo(
    GroupInfoDetailed group, 
    GroupMemberInfo member);

public record GroupInfoDetailed(
    GroupServerInfo[] servers,
    int allowed_servers_count,
    GroupRoleInfo[] roles,
    int id,
    string? name,
    string? description,
    int member_count,
    DateTime created_at,
    string type,
    string[] tags);

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
    string permissions,
    int role_id,
    DateTime created_at,
    string type);

public record InvitedGroupInfo(
    DateTime invited_at,
    GroupServerInfo[] servers,
    int allowed_server_count,
    GroupRoleInfo[] roles,
    int id,
    string? name,
    string? description,
    int member_count,
    DateTime created_at,
    string type,
    string[] tags);