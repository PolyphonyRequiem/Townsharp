using System.Text.Json;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public enum SubscriptionEventType
{
    GroupServerHeartbeat
}

public abstract record SubscriptionEvent (SubscriptionEventType SubscriptionEventType);

public record GroupServerHeartbeatEvent (GroupServerHeartbeatContent Content) : SubscriptionEvent(SubscriptionEventType.GroupServerHeartbeat);

public record GroupServerHeartbeatContent(
    int id,
    string name,
    UserInfo[] online_players,
    string server_status,
    string final_status, 
    int scene_index,
    int target,
    string region,
    DateTimeOffset online_ping,
    DateTimeOffset last_online,
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
    DateTimeOffset created_at,
    bool is_online,
    int transport_system);