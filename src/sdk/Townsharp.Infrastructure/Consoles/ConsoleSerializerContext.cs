using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Consoles.Models;

namespace Townsharp.Infrastructure.Subscriptions;

// Console Events
[JsonSerializable(typeof(PlayerMovedChunkEvent))]
[JsonSerializable(typeof(PlayerJoinedEvent))]
[JsonSerializable(typeof(PlayerLeftEvent))]

// Wire Messages
[JsonSerializable(typeof(ConsoleMessage))]
[JsonSerializable(typeof(CommandResponseMessage))]
[JsonSerializable(typeof(ConsoleSubscriptionEventMessage))]

// Request Message
[JsonSerializable(typeof(CommandRequestMessage))]

// Content
[JsonSerializable(typeof(PlayerInfo))]
internal partial class ConsoleSerializerContext : JsonSerializerContext
{

}