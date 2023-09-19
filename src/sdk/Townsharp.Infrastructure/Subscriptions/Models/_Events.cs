using System.Text.Json;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public record SubscriptionMessage(int id, string @event)
{
    public static SubscriptionMessage None = new(-1, string.Empty);
}

public record SubscriptionResponseMessage(int id, string content, int responseCode)
{
    public static SubscriptionResponseMessage None = new (-1, string.Empty, -1);
}


public record SubscriptionEventMessage (string @event, string key, JsonElement content)
{
    public static SubscriptionEventMessage None = new(string.Empty, string.Empty, new JsonElement());
}

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