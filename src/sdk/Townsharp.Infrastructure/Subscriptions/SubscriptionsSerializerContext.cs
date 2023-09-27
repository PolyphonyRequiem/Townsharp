using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

// Subscription Event Content
[JsonSerializable(typeof(ServerStatusContent))]
[JsonSerializable(typeof(GroupMemberUpdateContent))]
[JsonSerializable(typeof(GroupUpdateContent))]

// Wire Messages
[JsonSerializable(typeof(SubscriptionMessage))]
[JsonSerializable(typeof(SubscriptionResponseMessage))]
[JsonSerializable(typeof(SubscriptionEventMessage))]
[JsonSerializable(typeof(RequestMessage))]

// Wire Message Content
[JsonSerializable(typeof(MigrationTokenRequestContent))]
[JsonSerializable(typeof(BatchSubscriptionRequestContent))]
public partial class SubscriptionsSerializerContext : JsonSerializerContext
{

}