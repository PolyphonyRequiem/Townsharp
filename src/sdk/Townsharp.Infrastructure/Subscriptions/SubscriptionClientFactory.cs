using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionClientFactory
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<SubscriptionClientFactory> logger;

    public SubscriptionClientFactory(BotTokenProvider botTokenProvider, ILoggerFactory loggerFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<SubscriptionClientFactory>();
    }

    public async Task<SubscriptionClient> CreateAsync(ChannelWriter<SubscriptionEvent> channelWriter)
    {
        SubscriptionClient client;

        do
        {
            client = new SubscriptionClient(botTokenProvider, channelWriter, loggerFactory.CreateLogger<SubscriptionClient>());

            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogError($"{nameof(SubscriptionClient)} Error has occurred in {nameof(CreateAsync)}.  {ex}");
                // SMALL ISSUE HERE, WE WILL CLOSE THE CHANNEL IF WE DO THIS AS WRITTEN!  FINE IF THIS WORKS, BUT IF IT FAILS SOMEHOW WE IN TROUBLE!  CREATE THE WEBSOCKET FIRST?
                await client.DisposeAsync();
            }
        }
        while (!client.Ready);

        return client;
    }
}