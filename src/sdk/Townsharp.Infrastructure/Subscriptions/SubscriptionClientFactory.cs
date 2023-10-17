using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

internal class SubscriptionClientFactory
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly ILoggerFactory loggerFactory;

    internal SubscriptionClientFactory(BotTokenProvider botTokenProvider, ILoggerFactory loggerFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.loggerFactory = loggerFactory;
    }

    internal SubscriptionClient CreateClient(ChannelWriter<SubscriptionEvent> channelWriter)
    {
        return new SubscriptionClient(this.botTokenProvider, channelWriter, loggerFactory.CreateLogger<SubscriptionClient>());
    }
}