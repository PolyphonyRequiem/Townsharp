using System.Text.Json;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Consoles.Models;

public record ConsoleMessage(string type)
{
    public static ConsoleMessage None = new(string.Empty);
}

public record CommandResponseMessage(int commandId, JsonElement? data)
{
    public static CommandResponseMessage None = new(-1, default);
}

public record ConsoleSubscriptionEventMessage(string eventType, JsonElement? data)
{
    public static ConsoleSubscriptionEventMessage None = new(string.Empty, default);
}