using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Consoles.Models;

namespace Townsharp.Infrastructure.Subscriptions;

[JsonSerializable(typeof(ConsoleMessage))]
[JsonSerializable(typeof(CommandResponseMessage))]
[JsonSerializable(typeof(ConsoleSubscriptionEventMessage))]
[JsonSerializable(typeof(CommandRequestMessage))]
[JsonSerializable(typeof(PlayerMovedChunkEvent))]
[JsonSerializable(typeof(PlayerInfo))]
public partial class ConsoleSerializerContext : JsonSerializerContext
{

}