using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// Provides multiplexing of subscriptions against the main Alta Websocket Subscription endpoint across multiple connections.
/// This is done to ensure that connections are robust as the scale increases beyond ~500 subscriptions, so that migrations happen with a very high rate of success.
/// </summary>
public class SubscriptionMultiplexer : ISubscriptionClient
{
   private readonly SubscriptionMap subscriptionMap;
   private readonly Dictionary<ConnectionId, SubscriptionConnection> connections;
   private readonly ILogger<SubscriptionMultiplexer> logger;

   // Events
   /// <summary>
   /// Raised when a <see cref="SubscriptionEvent"/> is received.
   /// </summary>
   /// <remarks>
   /// Consider handling the subscription event using match syntax to handle the different event types.
   /// Evaluate <see cref="SubscriptionEvent.SubscriptionEventType"/> to determine the type of event, and then cast the <see cref="SubscriptionEvent"/> to the appropriate type using the 'as' keyword.
   /// </remarks>
   public event EventHandler<SubscriptionEvent>? SubscriptionEventReceived;

   private void RaiseOnSubscriptionEvent(SubscriptionEvent subscriptionEvent)
   {
      this.SubscriptionEventReceived?.Invoke(this, subscriptionEvent);
   }

   private SubscriptionMultiplexer(Dictionary<ConnectionId, SubscriptionConnection> connections, ILogger<SubscriptionMultiplexer> logger)
   {
      this.connections = connections;
      this.logger = logger;
      this.subscriptionMap = new SubscriptionMap(this.connections.Keys.ToArray());

      foreach (var subscriptionConnection in connections.Values)
      {
         subscriptionConnection.ReadAllEventsAsync(CancellationToken.None).ForEachAsync(e => this.RaiseOnSubscriptionEvent(e));
      }
   }

   internal static SubscriptionMultiplexer Create(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory, int concurrentConnections)
   {
      // TODO: Switch to something that auto-scales if at all possible.
      // That means that upon a fault that might lead to recovery, we should defer to the manager to determine if we should recover.
      // If we do want to recover, we simply notify the connection to proceed with recovery
      // Otherwise, we should subsume responsibility for the subscriptions, and remap them.
      // This should only occur on scale-in.

      var connectionIds = Enumerable.Range(0, concurrentConnections).Select(_ => new ConnectionId());
      var subscriptionConnections = connectionIds.Select(id => new SubscriptionConnection(id, subscriptionClientFactory, loggerFactory)).ToArray();
      var subscriptionConnectionsMap = subscriptionConnections.ToDictionary(connection => connection.ConnectionId);
      // TODO: Introduce Logging Scopes here
      return new SubscriptionMultiplexer(subscriptionConnectionsMap, loggerFactory.CreateLogger<SubscriptionMultiplexer>());
   }

   /// <summary>
   /// Starts running the <see cref="SubscriptionMultiplexer"/> asynchronously using the provided <paramref name="cancellationToken"/> to signal cancellation.
   /// </summary>
   /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to cancel the operation.</param>
   public async Task RunAsync(CancellationToken cancellationToken)
   {
      var tasks = this.connections.Values.Select(connection => connection.RunAsync(cancellationToken));
      await Task.WhenAll(tasks);
   }

   /// <summary>
   /// Registers a set of <see cref="SubscriptionDefinition"/>s to the <see cref="SubscriptionMultiplexer"/>, which will be multiplexed across the available connections.
   /// </summary>
   /// <param name="subscriptionDefinitions">The <see cref="SubscriptionDefinition"/>s to register.</param>
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

   /// <summary>
   /// Unregisters a set of <see cref="SubscriptionDefinition"/>s from the <see cref="SubscriptionMultiplexer"/>.
   /// </summary>
   /// <param name="subscriptionDefinitions">The <see cref="SubscriptionDefinition"/>s to unregister.</param>
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