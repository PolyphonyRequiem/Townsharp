using Microsoft.Extensions.Logging;

using Townsharp.Identity;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionClientFactory
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly ILoggerFactory loggerFactory;

    public SubscriptionClientFactory(BotTokenProvider botTokenProvider, ILoggerFactory loggerFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.loggerFactory = loggerFactory;
    }

    public async Task<SubscriptionClient> CreateAndConnectAsync()
    {
        return await SubscriptionClient.CreateAndConnectAsync(this.botTokenProvider, loggerFactory.CreateLogger<SubscriptionClient>());
    }
}