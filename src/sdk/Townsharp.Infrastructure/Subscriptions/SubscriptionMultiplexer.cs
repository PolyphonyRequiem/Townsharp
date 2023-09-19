using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionMultiplexer
{
    private readonly SubscriptionMap subscriptionMap;
    private readonly Dictionary<ConnectionId, SubscriptionConnection> connections;
    private readonly ILogger<SubscriptionMultiplexer> logger;
    private readonly int concurrentConnections;

    // Events
    public event EventHandler<SubscriptionEventMessage>? OnSubscriptionEvent;
    
    private void RaiseOnSubscriptionEvent(SubscriptionEventMessage subscriptionEvent)
    {
        this.OnSubscriptionEvent?.Invoke(this, subscriptionEvent);
    }

    internal SubscriptionMultiplexer(Dictionary<ConnectionId, SubscriptionConnection> connections, ILogger<SubscriptionMultiplexer> logger)
    {
        this.concurrentConnections = connections.Count;
        this.connections = connections;
        this.logger = logger;
        this.subscriptionMap = new SubscriptionMap(this.connections.Keys.ToArray());

        foreach (var subscriptionConnection in connections.Values)
        {
            subscriptionConnection.OnSubscriptionEvent += (sender, subscriptionEvent) => this.RaiseOnSubscriptionEvent(subscriptionEvent);
        }
    }

    internal static async Task<SubscriptionMultiplexer> CreateAsync(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory, int concurrentConnections)
    {
        // TODO: Switch to something that auto-scales if at all possible.
        // That means that upon a fault that might lead to recovery, we should defer to the manager to determine if we should recover.
        // If we do want to recover, we simply notify the connection to proceed with recovery
        // Otherwise, we should subsume responsibility for the subscriptions, and remap them.
        // This should only occur on scale-in.
        var connectionIds = Enumerable.Range(0, concurrentConnections).Select(_ => new ConnectionId());
        var initTasks = connectionIds.Select(id => SubscriptionConnection.CreateAsync(id, subscriptionClientFactory, loggerFactory));
        var subscriptionConnections = await Task.WhenAll(initTasks)!;
        var subscriptionConnectionsMap = subscriptionConnections.ToDictionary(connection => connection.ConnectionId);
        return new SubscriptionMultiplexer(subscriptionConnectionsMap, loggerFactory.CreateLogger<SubscriptionMultiplexer>());
    }

    public void RegisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        var newMappings = subscriptionMap.CreateSubscriptionMappingFor(subscriptionDefinitions);

        foreach (var mapping in newMappings)
        {
            this.logger.LogInformation($"Registering {mapping.Value.Length} subscriptions to connection {mapping.Key}.");
            var connection = this.connections[mapping.Key];
            connection.Subscribe(mapping.Value);
        }
    }

    public void UnregisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        var newMappings = subscriptionMap.CreateUnsubscriptionMappingFor(subscriptionDefinitions);

        foreach (var mapping in newMappings)
        {
            var connection = this.connections[mapping.Key];
            connection.Unsubscribe(mapping.Value);
        }
    }
}