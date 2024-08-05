using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

public interface ISubscriptionClient
{
   event EventHandler<SubscriptionEvent>? SubscriptionEventReceived;

   void RegisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions);

   Task ConnectAsync(CancellationToken cancellationToken);

   void UnregisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions);
}