using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Hosting;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Starting a SubscriptionManager test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Logging.AddConsole();
builder.Services.AddTownsharp();
builder.Services.AddHostedService<SubscriptionManagerTest>();

IHost host = builder.Build();
host.Run();

internal class SubscriptionManagerTest : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly BotTokenProvider botTokenProvider;
    private readonly SubscriptionManagerFactory subscriptionManagerFactory;
    private readonly ILogger<SubscriptionManagerTest> logger;

    private int totalCount = 0;
    private SubscriptionManager? subscriptionManager;

    public SubscriptionManagerTest(
        WebApiClient webApiClient,
        BotTokenProvider botTokenProvider,
        SubscriptionManagerFactory subscriptionManagerFactory,
        ILogger<SubscriptionManagerTest> logger)
    {
        this.webApiClient = webApiClient;
        this.botTokenProvider = botTokenProvider;
        this.subscriptionManagerFactory = subscriptionManagerFactory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.subscriptionManager = await this.subscriptionManagerFactory.CreateAsync();
        this.subscriptionManager.OnSubscriptionEvent += (sender, subscriptionEvent) =>
        {
            this.totalCount++;

            if (this.totalCount % 100 == 0)
            {
                logger.LogInformation($"Received {this.totalCount} events.");
            }

            logger.LogTrace($"Received Event - {subscriptionEvent.EventId}/{subscriptionEvent.KeyId} - {subscriptionEvent.Content.GetRawText()}");
        };

        var groupIds = await this.GetJoinedGroupIdsAsync(cancellationToken);

        var subscriptions = new[] { "group-server-heartbeat", "group-server-status", "group-update", "group-member-update" }
            .SelectMany(eventId => groupIds.Select(groupId => new SubscriptionDefinition(eventId, groupId)))
            .ToArray();

        this.subscriptionManager.RegisterSubscriptions(subscriptions);
    }

    private async Task<long[]> GetJoinedGroupIdsAsync(CancellationToken cancellationToken)
    {
        return await webApiClient.GetJoinedGroupsAsync()
            .Select(g => g!["group"]!["id"]!.GetValue<long>())
            .ToArrayAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            Environment.Exit(0);
        }

        return Task.CompletedTask;
    }
}