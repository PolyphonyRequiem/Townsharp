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

    public async Task<SubscriptionClient> CreateAndConnectAsync()
    {
        return await SubscriptionClient.CreateAndConnectAsync(this.botTokenProvider, loggerFactory.CreateLogger<SubscriptionClient>());
    }
}