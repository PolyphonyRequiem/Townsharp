using Townsharp.Identity;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionClientFactory
{
    private readonly BotTokenProvider botTokenProvider;

    public SubscriptionClientFactory(BotTokenProvider botTokenProvider)
    {
        this.botTokenProvider = botTokenProvider;
    }

    public async Task<SubscriptionClient> CreateAndConnectAsync()
    {
        return await SubscriptionClient.CreateAndConnectAsync(this.botTokenProvider);
    }
}