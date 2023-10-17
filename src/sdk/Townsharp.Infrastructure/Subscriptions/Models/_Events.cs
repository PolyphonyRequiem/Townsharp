using System.Text.Json;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public enum SubscriptionEventType
{
    GroupServerHeartbeat,
    GroupServerStatus,
    GroupUpdate,
    GroupMemberUpdate
}

public abstract record SubscriptionEvent (SubscriptionEventType SubscriptionEventType)
{
    internal static SubscriptionEvent FromEventMessage(SubscriptionEventMessage message)
    {
        return message.@event switch
        {
            "group-server-heartbeat" => DeserializeUsingContext<GroupServerHeartbeatEvent, ServerStatusContent>(message.content, content => new GroupServerHeartbeatEvent(content)),
            "group-server-status" => DeserializeUsingContext<GroupServerStatusChangedEvent, ServerStatusContent>(message.content, content => new GroupServerStatusChangedEvent(content)),
            "group-member-update" => DeserializeUsingContext<GroupMemberUpdateEvent, GroupMemberUpdateContent>(message.content, content => new GroupMemberUpdateEvent(content)),
            "group-update" => DeserializeUsingContext<GroupUpdateEvent, GroupUpdateContent>(message.content, content => new GroupUpdateEvent(content)),
            _ => throw new NotImplementedException(),
        };
    }

    private static TSubscriptionEvent DeserializeUsingContext<TSubscriptionEvent, TEventContent>(string content, Func<TEventContent, TSubscriptionEvent> eventFactory)
        where TSubscriptionEvent : SubscriptionEvent
    {
        return JsonSerializer.Deserialize(content, typeof(TEventContent), SubscriptionsSerializerContext.Default) switch
        {
            TEventContent c => eventFactory(c),
            _ => throw new InvalidOperationException($"Unable to deserialize {typeof(TEventContent)}")
        };
    }
}

public record GroupUpdateEvent(GroupUpdateContent Content) : SubscriptionEvent(SubscriptionEventType.GroupUpdate);

public record GroupUpdateContent(
    int id,
    string name,
    string description,
    int member_count,
    DateTimeOffset created_at,
    string type,
    string[] tags);

public record GroupMemberUpdateEvent(GroupMemberUpdateContent Content) : SubscriptionEvent(SubscriptionEventType.GroupMemberUpdate);

public record GroupMemberUpdateContent(
    int group_id,
    int user_id,
    string username,
    bool bot,
    int icon,
    string permissions,
    int role_id,
    DateTimeOffset created_at,
    string type);

public record GroupServerStatusChangedEvent(ServerStatusContent Content) : SubscriptionEvent(SubscriptionEventType.GroupServerStatus);

public record GroupServerHeartbeatEvent (ServerStatusContent Content) : SubscriptionEvent(SubscriptionEventType.GroupServerHeartbeat);

public record ServerStatusContent(
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