namespace Townsharp.Infrastructure.Subscriptions.Models;

internal record RequestMessage
{
    internal int id { get; init; }

    internal string path { get; init; }

    internal string method { get; init; }

    internal string authorization { get; init; }

    internal object? content { get; init; }

    private RequestMessage(int id, string path, string method, string token, object? content = default)
    {
        this.id = id;
        this.path = path;
        this.method = method;
        this.authorization = $"Bearer {token}";
        this.content = content;
    }

    internal static RequestMessage CreateSubscriptionRequestMessage(int id, string token, string eventId, int eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "POST", token);

    internal static RequestMessage CreateUnsubscriptionRequestMessage(int id, string token, string eventId, int eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "DELETE", token);

    internal static RequestMessage CreateBatchSubscriptionRequestMessage(int id, string token, string eventId, int[] eventKeys)
        => new(id, $"subscription/batch", "POST", token, new BatchSubscriptionRequestContent[] { new(eventId, eventKeys) }); // single element cuz of noted bugs

    internal static RequestMessage CreateGetMigrationTokenRequestMessage(int id, string token)
        => new(id, $"migrate", "GET", token);

    internal static RequestMessage CreateSendMigrationTokenRequestMessage(int id, string token, string migrationToken)
        => new(id, $"migrate", "POST", token, new MigrationTokenRequestContent(migrationToken));
}

internal record MigrationTokenRequestContent(string token);

internal record BatchSubscriptionRequestContent(string @event, string[] keys)
{
    internal BatchSubscriptionRequestContent(string @event, int[] keys)
        : this(@event, keys.Select(k => k.ToString()).ToArray())
    {

    }
}

