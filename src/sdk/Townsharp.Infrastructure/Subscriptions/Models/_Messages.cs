using System.Text.Json;

namespace Townsharp.Infrastructure.Subscriptions.Models;

internal record SubscriptionMessage(int id, string @event)
{
    internal static SubscriptionMessage None = new(-1, string.Empty);
}

internal record SubscriptionResponseMessage(int id, string key, string content, int responseCode)
{
    internal static SubscriptionResponseMessage None = new(-1, string.Empty, string.Empty, -1);
}

internal record SubscriptionEventMessage(string @event, string key, string content)
{
    internal static SubscriptionEventMessage None = new(string.Empty, string.Empty, string.Empty);
}

internal record MigrationToken(string Token)
{
    internal static MigrationToken None { get; set; } = new(string.Empty);

    internal static MigrationToken FromContent(string content) 
    {
        using var document = JsonDocument.Parse(content);
        return new MigrationToken(document.RootElement.GetProperty("token").GetString() ?? string.Empty);
    }
}

internal record InfrastructureError(string message, string connectionId, string requestId);