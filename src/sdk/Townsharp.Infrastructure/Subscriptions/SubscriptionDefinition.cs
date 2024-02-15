namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// A definition of a subscription to an event.
/// </summary>
/// <param name="EventId">The Id of the event to subscribe to (for example "group-server-heartbeat") see the documentation on subscriptions for more details.</param>
/// <param name="KeyId">The key id of the subscription to subscribe to.  This is the number after the last "/" in the subscription path. For "me" subscriptions, this is the bot's user id, otherwise it is the group id.</param>
public record SubscriptionDefinition(string EventId, int KeyId)
{
    internal static SubscriptionDefinition Parse(string eventPath)
    {
        var parts = eventPath.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid subscription definition format.  Expected <event>/<key>", nameof(eventPath));
        }

        return new SubscriptionDefinition(parts[0], int.Parse(parts[1]));
    }

    public override string ToString() => $"{EventId}/{KeyId}";
}