using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions.Models;

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

    internal SubscriptionMessageClient CreateClient(ChannelWriter<SubscriptionEvent> channelWriter)
    {
        return new SubscriptionMessageClient(this.botTokenProvider, channelWriter, loggerFactory.CreateLogger<SubscriptionMessageClient>());
    }
}