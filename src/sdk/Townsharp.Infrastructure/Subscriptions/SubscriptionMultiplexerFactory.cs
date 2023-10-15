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

    public SubscriptionMultiplexer Create(int concurrentConnections = DEFAULT_MAX_CONNECTIONS)
    {
        return SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);
    }
}
