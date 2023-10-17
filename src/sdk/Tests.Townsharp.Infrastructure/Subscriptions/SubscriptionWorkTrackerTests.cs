using Microsoft.Extensions.Logging;

using NSubstitute;

using Townsharp.Infrastructure.Subscriptions;

namespace Tests.Townsharp.Infrastructure.Subscriptions;
public class SubscriptionWorkTrackerTests
{
    [Fact]
    public void RegisterSubscriptionsLeaseAndResolveWork()
    {
        var tracker = new SubscriptionWorkTracker(Substitute.For<ILogger>());

        var subscriptions = new SubscriptionDefinition[]
        {
            new SubscriptionDefinition("a", 1),
            new SubscriptionDefinition("b", 2),
            new SubscriptionDefinition("c", 3),
            new SubscriptionDefinition("d", 4),
            new SubscriptionDefinition("e", 5),
        };

        tracker.AddSubscriptions(subscriptions);

        var leases = tracker.TakeWorkLeases(3);
        Assert.Equal(3, leases.Count());

        foreach(var lease in leases)
        {
            tracker.ReportLeaseResolved(lease);
        }
    }
}
