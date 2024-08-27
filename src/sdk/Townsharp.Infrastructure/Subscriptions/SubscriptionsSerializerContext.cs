using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Models;
using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

// Subscription Event Content
[JsonSerializable(typeof(ServerInfo))]
[JsonSerializable(typeof(GroupMemberUpdateContent))]
[JsonSerializable(typeof(GroupUpdateContent))]
[JsonSerializable(typeof(JoinedGroupInfo))]
[JsonSerializable(typeof(GroupInfoDetailed))]
[JsonSerializable(typeof(GroupServerInfo))]
[JsonSerializable(typeof(GroupRoleInfo))]
[JsonSerializable(typeof(GroupMemberInfo))]


// Wire Messages
[JsonSerializable(typeof(SubscriptionMessage))]
[JsonSerializable(typeof(SubscriptionResponseMessage))]
[JsonSerializable(typeof(SubscriptionEventMessage))]
[JsonSerializable(typeof(RequestMessage))]
[JsonSerializable(typeof(MigrationTokenRequestContent))]
[JsonSerializable(typeof(BatchSubscriptionRequestContent))]

// Wire Message Content
[JsonSerializable(typeof(MigrationTokenRequestContent))]
[JsonSerializable(typeof(BatchSubscriptionRequestContent))]
internal partial class SubscriptionsSerializerContext : JsonSerializerContext
{

}