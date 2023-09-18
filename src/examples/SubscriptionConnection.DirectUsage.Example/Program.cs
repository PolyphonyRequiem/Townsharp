using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Hosting;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Starting a SubscriptionConnection test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Services.AddHttpClient();
builder.Services.AddTownsharpInfra();

builder.Logging.AddConsole();
//builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddHostedService<SubscriptionConnectionTest>();

IHost host = builder.Build();
host.Run();

internal class SubscriptionConnectionTest : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly ILogger<SubscriptionConnectionTest> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private SubscriptionConnection? subscriptionConnection;

    public SubscriptionConnectionTest(
        WebApiClient webApiClient,
        ILogger<SubscriptionConnectionTest> logger, 
        ILoggerFactory loggerFactory, 
        SubscriptionClientFactory subscriptionClientFactory)
    {
        this.webApiClient = webApiClient;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.subscriptionClientFactory = subscriptionClientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.subscriptionConnection = await SubscriptionConnection.CreateAsync(new ConnectionId(), this.subscriptionClientFactory, this.loggerFactory);

        _ = Task.Run(() => SubscribeToGroupsAsync(cancellationToken), cancellationToken);

        this.subscriptionConnection.OnSubscriptionEvent += (sender, subscriptionEvent) =>
        {
            logger.LogInformation($"Received Event - {subscriptionEvent.EventId}/{subscriptionEvent.KeyId} - {subscriptionEvent.Content.GetRawText()}");
        };
    }
    
    private async Task SubscribeToGroupsAsync(CancellationToken cancellationToken)
    {
        var groupIds = await this.GetJoinedGroupIdsAsync(cancellationToken);
        this.subscriptionConnection!.Subscribe(groupIds.Take(500).Select(id => new SubscriptionDefinition("group-server-heartbeat", id)).ToArray());
    }

    private async Task<int[]> GetJoinedGroupIdsAsync(CancellationToken cancellationToken)
    {
        return await webApiClient.GetJoinedGroupsAsync()
            .Select(g => g!["group"]!["id"]!.GetValue<int>())
            .ToArrayAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            Environment.Exit(0);
        }

        await this.subscriptionConnection!.DisposeAsync();
    }
}