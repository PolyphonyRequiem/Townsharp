using System.Text.Json;

namespace Townsharp.Infrastructure.Consoles;

public record ConsoleMessage(string type)
{
    internal static ConsoleMessage None = new(string.Empty);
}

public record CommandResponseMessage(int commandId, JsonElement? data)
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

public record ConsoleSubscriptionEventMessage(string eventType, JsonElement? data)
{
    internal static ConsoleSubscriptionEventMessage None = new(string.Empty, default);
}