﻿using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

[JsonSerializable(typeof(GroupServerHeartbeatEvent))]
[JsonSerializable(typeof(GroupServerHeartbeatContent))]
[JsonSerializable(typeof(SubscriptionMessage))]
[JsonSerializable(typeof(SubscriptionResponseMessage))]
[JsonSerializable(typeof(SubscriptionEventMessage))]
[JsonSerializable(typeof(RequestMessage))]
[JsonSerializable(typeof(MigrationTokenRequestContent))]
[JsonSerializable(typeof(BatchSubscriptionRequestContent))]
public partial class SubscriptionsSerializerContext : JsonSerializerContext
{

}