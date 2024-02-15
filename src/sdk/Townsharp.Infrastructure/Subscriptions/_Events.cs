using System.Text.Json;

using Townsharp.Infrastructure.Models;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public enum SubscriptionEventType
{
    GroupMemberUpdate, // group-member-update
    // GroupServerCreate, // group-server-create
    // GroupServerDelete, // group-server-delete
    GroupServerHeartbeat, // group-server-heartbeat
    GroupServerStatus, // group-server-status
    // GroupServerUpdate, // group-server-update
    GroupUpdate, // group-update
    JoinedGroup, // me-group-create
    LeftGroup, // me-group-delete
    InvitedToGroup, // me-group-invite-create
    UninvitedFromGroup, // me-group-invite-delete
}

public abstract record SubscriptionEvent(SubscriptionEventType SubscriptionEventType)
{
    internal static SubscriptionEvent FromEventMessage(SubscriptionEventMessage message)
    {
        return message.@event switch
        {
            "group-server-heartbeat" => DeserializeUsingContext<GroupServerHeartbeatEvent, ServerInfo>(message.content, content => new GroupServerHeartbeatEvent(content)),
            "group-server-status" => DeserializeUsingContext<GroupServerStatusChangedEvent, ServerInfo>(message.content, content => new GroupServerStatusChangedEvent(content)),
            "group-member-update" => DeserializeUsingContext<GroupMemberUpdateEvent, GroupMemberUpdateContent>(message.content, content => new GroupMemberUpdateEvent(content)),
            "group-update" => DeserializeUsingContext<GroupUpdateEvent, GroupUpdateContent>(message.content, content => new GroupUpdateEvent(content)),
            "me-group-create" => DeserializeUsingContext<JoinedGroupEvent, JoinedGroupInfo>(message.content, content => new JoinedGroupEvent(content)),
            "me-group-delete" => DeserializeUsingContext<LeftGroupEvent, JoinedGroupInfo>(message.content, content => new LeftGroupEvent(content)),
            "me-group-invite-create" => DeserializeUsingContext<InvitedToGroupEvent, GroupInfoDetailed>(message.content, content => new InvitedToGroupEvent(content)),
            "me-group-invite-delete" => DeserializeUsingContext<UninvitedFromGroupEvent, GroupInfoDetailed>(message.content, content => new UninvitedFromGroupEvent(content)),
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

public record GroupServerHeartbeatEvent(ServerInfo Content) : SubscriptionEvent(SubscriptionEventType.GroupServerHeartbeat);

public record GroupServerStatusChangedEvent(ServerInfo Content) : SubscriptionEvent(SubscriptionEventType.GroupServerStatus);

public record GroupUpdateEvent(GroupUpdateContent Content) : SubscriptionEvent(SubscriptionEventType.GroupUpdate);

public record GroupUpdateContent(
    int id,
    string name,
    string description,
    int member_count,
    DateTimeOffset created_at,
    string type,
    string[] tags);

public record JoinedGroupEvent(JoinedGroupInfo Content) : SubscriptionEvent(SubscriptionEventType.JoinedGroup);

public record LeftGroupEvent(JoinedGroupInfo Content) : SubscriptionEvent(SubscriptionEventType.LeftGroup);

public record InvitedToGroupEvent(GroupInfoDetailed Content) : SubscriptionEvent(SubscriptionEventType.InvitedToGroup);

public record UninvitedFromGroupEvent(GroupInfoDetailed Content) : SubscriptionEvent(SubscriptionEventType.UninvitedFromGroup);