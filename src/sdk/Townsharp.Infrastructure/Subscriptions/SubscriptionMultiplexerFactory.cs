using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionMultiplexerFactory
{
    private const int DEFAULT_MAX_CONNECTIONS = 10;

    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;

    public SubscriptionMultiplexerFactory(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
    }

    public async Task<SubscriptionMultiplexer> CreateAsync(int concurrentConnections = DEFAULT_MAX_CONNECTIONS)
    {
        return await SubscriptionMultiplexer.CreateAsync(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);
    }
}
