using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions.Models;

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

    public async Task<SubscriptionManager> CreateAsync(ChannelWriter<SubscriptionEvent> channelWriter)
    {
        return await SubscriptionManager.CreateAsync(this.subscriptionClientFactory, channelWriter, this.loggerFactory);
    }
}
