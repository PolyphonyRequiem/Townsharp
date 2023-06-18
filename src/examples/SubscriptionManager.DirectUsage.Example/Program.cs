using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Identity.Models;
using Townsharp.Infrastructure.Logging;
using Townsharp.Infrastructure.Subscriptions;

Console.WriteLine("Starting a SubscriptionManager test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Services.AddHttpClient();
builder.Services.AddSingleton(
    services => new BotTokenProvider(
           new BotCredential(
               Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID")!,
               Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET")!),
           services.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton<SubscriptionClientFactory>();
builder.Logging.AddConsole();
builder.Services.AddHostedService<SubscriptionManagerTest>();

IHost host = builder.Build();

// use the logger configuration from the host
TownsharpLogging.LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

host.Run();

internal class SubscriptionManagerTest : IHostedService
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly ILogger<SubscriptionManagerTest> logger;
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private SubscriptionManager? subscriptionManager;

    private int totalCount = 0;

    public SubscriptionManagerTest(
        BotTokenProvider botTokenProvider,
        ILogger<SubscriptionManagerTest> logger,
        SubscriptionClientFactory subscriptionClientFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.logger = logger;
        this.subscriptionClientFactory = subscriptionClientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.subscriptionManager = await SubscriptionManager.CreateAsync(this.subscriptionClientFactory);
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