using System.Text.Json;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Consoles;

internal record ConsoleMessage(string type)
{
    internal static ConsoleMessage None = new(string.Empty);
}

public record CommandResponseMessage(int commandId, JsonNode? data)
{
    private static JsonSerializerOptions options = new()
    {
        WriteIndented = true
    };

    internal static CommandResponseMessage None = new(-1, default);

    public override string ToString()
    {
        return JsonSerializer.Serialize(data, options);
    }
}

internal record ConsoleSubscriptionEventMessage(string eventType, JsonNode? data)
{
    internal static ConsoleSubscriptionEventMessage None = new(string.Empty, default);
}