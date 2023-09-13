namespace Townsharp.Infrastructure.Subscriptions.Models;

public record RequestMessage
{
    public int id { get; init; }

    public string path { get; init; }
    
    public string method { get; init; }
    
    public string authorization { get; init; }
    
    public object? content { get; init; }

    private RequestMessage(int id, string path, string method, string token, object? content = default )        
    { 
        this.id = id;
        this.path = path;
        this.method = method;
        this.authorization = $"Bearer {token}";
        this.content = content;
    }

    public static RequestMessage CreateSubscriptionRequestMessage(int id, string token, string eventId, int eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "POST", token);

    public static RequestMessage CreateUnsubscriptionRequestMessage(int id, string token, string eventId, int eventKey)
        => new(id, $"subscription/{eventId}/{eventKey}", "DELETE", token);

    public static RequestMessage CreateBatchSubscriptionRequestMessage(int id, string token, string eventId, int[] eventKeys)
        => new(id, $"subscription/batch", "POST", token, new BatchSubscriptionContentElement[] { new (eventId, eventKeys)}); // single element cuz of noted bugs

    public static RequestMessage CreateGetMigrationTokenRequestMessage(int id, string token)
        => new(id, $"migrate", "GET", token);

    public static RequestMessage CreateSendMigrationTokenRequestMessage(int id, string token, string migrationToken)
        => new(id, $"migrate", "POST", token, new {token=migrationToken});
}

file record struct BatchSubscriptionContentElement(string @event, string[] keys)
{
    public BatchSubscriptionContentElement(string @event, int[] keys)
        : this(@event, keys.Select(k => k.ToString()).ToArray())
    {

    }
}
