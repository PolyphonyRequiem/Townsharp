using System.Text.Json;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public record Message(int id, string @event, string key, string content, int responseCode)
{
    public static Message None => NoEventMessage.Instance;
}

file record NoEventMessage : Message
{
    public static NoEventMessage Instance = new();

    private NoEventMessage() : base(0, string.Empty, string.Empty, string.Empty, 0)
    {

    }
}

public record SubscriptionEvent
{
    public string @event { get; set; }

    public string key { get; init; }

    public JsonElement content { get; init; }

    public SubscriptionEvent(string @event, string key, JsonElement content)
    {
        this.@event = @event;
        this.key = key;
        this.content = content;
    }

    public static SubscriptionEvent Create(Message eventMessage)
    {
        return new SubscriptionEvent(eventMessage.@event, eventMessage.key, JsonSerializer.Deserialize<JsonElement>(eventMessage.content));
    }
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