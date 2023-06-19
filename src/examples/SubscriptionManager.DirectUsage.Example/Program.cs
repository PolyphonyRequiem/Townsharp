using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Hosting;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Subscriptions.Models;

Console.WriteLine("Starting a SubscriptionManager test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Logging.AddConsole();
builder.Services.AddTownsharp();
builder.Services.AddHostedService<SubscriptionManagerTest>();

IHost host = builder.Build();
host.Run();

internal class SubscriptionManagerTest : IHostedService
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly SubscriptionManagerFactory subscriptionManagerFactory;
    private readonly ILogger<SubscriptionManagerTest> logger;

    private int totalCount = 0;
    private SubscriptionManager? subscriptionManager;

    public SubscriptionManagerTest(
        BotTokenProvider botTokenProvider,
        SubscriptionManagerFactory subscriptionManagerFactory,
        ILogger<SubscriptionManagerTest> logger)
    {
        this.botTokenProvider = botTokenProvider;
        this.subscriptionManagerFactory = subscriptionManagerFactory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<SubscriptionEvent>();

        this.subscriptionManager = await this.subscriptionManagerFactory.CreateAsync(channel.Writer);

        _ = Task.Run(async () =>
        {
            await foreach (var subscriptionEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                this.totalCount++;

                if (this.totalCount % 100 == 0)
                {
                    logger.LogInformation($"Received {this.totalCount} events.");
                }

                logger.LogTrace($"Received Event - {subscriptionEvent.EventId}/{subscriptionEvent.KeyId} - {subscriptionEvent.Content.GetRawText()}");
            }
        });

        var groupIds = await this.GetJoinedGroupIdsAsync(cancellationToken);

        var subscriptions = new[] { "group-server-heartbeat", "group-server-status", "group-update", "group-member-update" }
            .SelectMany(
                eventId => groupIds
                    .Select(groupId => new SubscriptionDefinition(eventId, groupId)))
            .ToArray();

        this.subscriptionManager.RegisterSubscriptions(subscriptions);
    }

    private async Task<long[]> GetJoinedGroupIdsAsync(CancellationToken cancellationToken)
    {
        var apiClient = new HttpClient();
        apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {await this.botTokenProvider.GetTokenAsync(CancellationToken.None)}");
        var joinedGroups = await apiClient.GetFromJsonAsync<JsonDocument>("https://webapi.townshiptale.com/api/groups/joined?limit=2000", cancellationToken);
        var groupsIds = joinedGroups!.RootElement.EnumerateArray().Select(g => g.GetProperty("group").GetProperty("id").GetInt64()).ToArray();

        return groupsIds;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        if (cancellationToken.IsCancellationRequested)
        {
            Environment.Exit(0);
        }

        //await this.subscriptionManager!.DisposeAsync();
    }
}