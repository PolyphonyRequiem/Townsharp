namespace Townsharp.Infrastructure.Subscriptions.Models;

public record RequestMessage
{
    public long id { get; init; }

    public string path { get; init; }
    
    public string method { get; init; }
    
    public string authorization { get; init; }
    
    public object? content { get; init; }

    private RequestMessage(long id, string path, string method, string token, object? content = default )        
    { 
        this.id = id;
        this.path = path;
        this.method = method;
        this.authorization = $"Bearer {token}";
        this.content = content;
    }

    public static RequestMessage CreateSubscriptionRequestMessage(long id, string token, string eventId, long eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "POST", token);

    public static RequestMessage CreateUnsubscriptionRequestMessage(long id, string token, string eventId, long eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "DELETE", token);

    public static RequestMessage CreateBatchSubscriptionRequestMessage(long id, string token, string eventId, long[] eventKeys)
        => new(id, $"subscription/batch", "POST", token, new BatchSubscriptionContentElement[] { new (eventId, eventKeys)}); // single element cuz of noted bugs

    public static RequestMessage CreateGetMigrationTokenRequestMessage(long id, string token)
        => new(id, $"migrate", "GET", token);

    public static RequestMessage CreateSendMigrationTokenRequestMessage(long id, string token, string migrationToken)
        => new(id, $"migrate", "POST", token, new {token=migrationToken});
}

file record struct BatchSubscriptionContentElement(string @event, string[] keys)
{
    public BatchSubscriptionContentElement(string @event, long[] keys)
        : this(@event, keys.Select(k => k.ToString()).ToArray())
    {

    }
}
