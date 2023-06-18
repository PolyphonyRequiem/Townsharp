using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Subscriptions.Models;

public class SubscriptionManager
{
    private readonly SubscriptionMap subscriptionMap;
    private readonly Dictionary<ConnectionId, SubscriptionConnection> connections;
    private readonly ILogger<SubscriptionManager> logger;

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
        var connectionIds = Enumerable.Range(0, 10).Select(_ => new ConnectionId());
        var initTasks = connectionIds.Select(id => SubscriptionConnection.CreateAsync(id, subscriptionClientFactory, loggerFactory.CreateLogger<SubscriptionConnection>()));
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
        throw new NotImplementedException();
        var newMappings = subscriptionMap.CreateUnsubscriptionMappingFor(subscriptionDefinitions);

        foreach (var mapping in newMappings)
        {
            var connection = this.connections[mapping.Key];
            connection.Subscribe(mapping.Value);
        }
    }
}