using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionManagerFactory
{
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;

    public SubscriptionManagerFactory(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
    }

    public async Task<SubscriptionManager> CreateAsync()
    {
        return await SubscriptionManager.CreateAsync(this.subscriptionClientFactory, this.loggerFactory);
    }
}
