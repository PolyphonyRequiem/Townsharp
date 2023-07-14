using System.Formats.Asn1;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Subscriptions.Models;

public class SubscriptionManager
{
    private readonly SubscriptionMap subscriptionMap;
    private readonly Dictionary<ConnectionId, SubscriptionConnection> connections;
    private readonly ILogger<SubscriptionManager> logger;
    private const int DEFAULT_MAX_CONNECTIONS = 10;

    // Events
    public event EventHandler<SubscriptionEvent>? OnSubscriptionEvent;
    
    private void RaiseOnSubscriptionEvent(SubscriptionEvent subscriptionEvent)
    {
        this.OnSubscriptionEvent?.Invoke(this, subscriptionEvent);
    }

    protected SubscriptionManager(Dictionary<ConnectionId, SubscriptionConnection> connections, ILogger<SubscriptionManager> logger)
    {
        this.connections = connections;
        this.logger = logger;
        this.subscriptionMap = new SubscriptionMap(this.connections.Keys.ToArray());

        foreach (var subscriptionConnection in connections.Values)
        {
            subscriptionConnection.OnSubscriptionEvent += (sender, subscriptionEvent) => this.RaiseOnSubscriptionEvent(subscriptionEvent);
        }
    }

    public static async Task<SubscriptionManager> CreateAsync(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        // TODO: Switch to something that auto-scales if at all possible.
        // That means that upon a fault that might lead to recovery, we should defer to the manager to determine if we should recover.
        // If we do want to recover, we simply notify the connection to proceed with recovery
        // Otherwise, we should subsume responsibility for the subscriptions, and remap them.
        // This should only occur on scale-in.
        var connectionIds = Enumerable.Range(0, DEFAULT_MAX_CONNECTIONS).Select(_ => new ConnectionId());
        var initTasks = connectionIds.Select(id => SubscriptionConnection.CreateAsync(id, subscriptionClientFactory, loggerFactory));
        var subscriptionConnections = await Task.WhenAll(initTasks)!;
        var subscriptionConnectionsMap = subscriptionConnections.ToDictionary(connection => connection.ConnectionId);
        return new SubscriptionManager(subscriptionConnectionsMap, loggerFactory.CreateLogger<SubscriptionManager>());
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