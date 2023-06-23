namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionMap
{
    private const int SuggestedSubscriptionsPerConnectionLimit = 1000;
    private readonly Dictionary<ConnectionId, HashSet<SubscriptionDefinition>> connectionMappings;

    // Without resorting to immutable collections and other weirdness, we need to lock the connection mappings when we are updating them.
    private readonly object mappingLock = new object();

    public SubscriptionMap(ConnectionId[] connectionIds)
    {
        this.connectionMappings = new Dictionary<ConnectionId, HashSet<SubscriptionDefinition>>(
            connectionIds.ToDictionary(
                id => id,
                _ => new HashSet<SubscriptionDefinition>(SuggestedSubscriptionsPerConnectionLimit)));
    }

    public IReadOnlyDictionary<ConnectionId, HashSet<SubscriptionDefinition>> ConnectionMappings => this.connectionMappings.AsReadOnly();

    
    // Make this a custom type.  Doesn't really need to be a dictionary and in fact that obscures the intent
    public Dictionary<ConnectionId, SubscriptionDefinition[]> CreateSubscriptionMappingFor(SubscriptionDefinition[] definitions)
    {
        lock (mappingLock)
        {
            var newMappings = new Dictionary<ConnectionId, List<SubscriptionDefinition>>();

            // Get counts of subscriptions for each connection
            var counts = this.connectionMappings
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);

            for (int i = 0; i < definitions.Length; i++)
            {
                var newDefinition = definitions[i];
                // First check if this is already mapped. If so, skip it.

                if (IsMapped(newDefinition))
                {
                    continue;
                }

                // Find the connection with the least number of subscriptions
                // This is horribly slow, but not slow enough to matter for our usage.
                var targetConnectionId = counts
                    .OrderBy(kvp => kvp.Value)
                    .First().Key;

                // Add the new subscription to the connection
                this.connectionMappings[targetConnectionId].Add(newDefinition);

                // Update the count
                counts[targetConnectionId]++;

                // Add to the new mappings result
                if (newMappings.ContainsKey(targetConnectionId))
                {
                    newMappings[targetConnectionId].Add(newDefinition);
                }
                else
                {
                    newMappings[targetConnectionId] = new List<SubscriptionDefinition> { newDefinition };
                }
            }

            return newMappings.ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value.ToArray());
        }
    }

    public Dictionary<ConnectionId, SubscriptionDefinition[]> CreateUnsubscriptionMappingFor(SubscriptionDefinition[] definitions)
    {
        lock (mappingLock)
        {
            var newMappings = new Dictionary<ConnectionId, List<SubscriptionDefinition>>();

            foreach (var definition in definitions)
            {
                foreach (var mapping in this.connectionMappings)
                {
                    if (mapping.Value.Contains(definition))
                    {
                        var connectionId = mapping.Key;
                        if (this.connectionMappings[connectionId].Remove(definition))
                        {
                            if (newMappings.ContainsKey(connectionId))
                            {
                                newMappings[connectionId].Add(definition);
                            }
                            else
                            {
                                newMappings[connectionId] = new List<SubscriptionDefinition> { definition };
                            }
                        }
                    }
                }
            }

            return newMappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        }
    }

    private bool IsMapped(SubscriptionDefinition subscriptionDefinition) => 
        this.connectionMappings.Any(connectionMappings => connectionMappings.Value.Contains(subscriptionDefinition));
}