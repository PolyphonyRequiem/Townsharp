using System.Text.Json;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Consoles.Models;

public record ConsoleMessage(string type)
{
    internal static ConsoleMessage None = new(string.Empty);
}

public record CommandResponseMessage(int commandId, JsonElement? data)
{
    internal static CommandResponseMessage None = new(-1, default);
}

public record ConsoleSubscriptionEventMessage(string eventType, JsonElement? data)
{
    internal static ConsoleSubscriptionEventMessage None = new(string.Empty, default);
}