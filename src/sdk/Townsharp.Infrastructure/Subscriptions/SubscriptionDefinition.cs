namespace Townsharp.Infrastructure.Subscriptions;

public record SubscriptionDefinition(string EventId, int KeyId)
{
    public static SubscriptionDefinition Parse(string eventPath)
    {
        var parts = eventPath.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid subscription definition format.  Expected <event>/<key>", nameof(eventPath));
        }

        return new SubscriptionDefinition(parts[0], int.Parse(parts[1]));
    }

    override public string ToString() => $"{EventId}/{KeyId}";
}