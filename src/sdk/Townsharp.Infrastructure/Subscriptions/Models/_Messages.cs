using System.Text.Json;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public record SubscriptionMessage(int id, string @event)
{
    public static SubscriptionMessage None = new(-1, string.Empty);
}

public record SubscriptionResponseMessage(int id, string key, string content, int responseCode)
{
    public static SubscriptionResponseMessage None = new(-1, string.Empty, string.Empty, -1);
}

public record SubscriptionEventMessage(string @event, string key, string content)
{
    public static SubscriptionEventMessage None = new(string.Empty, string.Empty, string.Empty);
}

public record MigrationToken(string Token)
{
    public static MigrationToken None { get; internal set; } = new(string.Empty);

    public static MigrationToken FromContent(string content) 
    {
        using var document = JsonDocument.Parse(content);
        return new MigrationToken(document.RootElement.GetProperty("token").GetString() ?? string.Empty);
    }
}