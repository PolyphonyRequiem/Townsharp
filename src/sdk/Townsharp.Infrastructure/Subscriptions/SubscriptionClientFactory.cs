using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionClientFactory
{
    private readonly IBotTokenProvider botTokenProvider;
    private readonly ILoggerFactory loggerFactory;

    public SubscriptionClientFactory(IBotTokenProvider botTokenProvider, ILoggerFactory loggerFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.loggerFactory = loggerFactory;
    }

    internal SubscriptionMessageClient CreateClient()
    {
        return new SubscriptionMessageClient(this.botTokenProvider, loggerFactory.CreateLogger<SubscriptionMessageClient>());
    }
}