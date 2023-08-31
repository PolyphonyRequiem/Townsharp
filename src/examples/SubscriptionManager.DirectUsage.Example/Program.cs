using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Townsharp.Infrastructure.Hosting;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

string ServiceName = Assembly.GetExecutingAssembly().GetName().Name ?? "Unknown Assembly";
ActivitySource ActivitySource = new ActivitySource(ServiceName);

Console.WriteLine("Starting a SubscriptionManager test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();



builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(loggerOptions =>
    {
        loggerOptions.AddOtlpExporter(); 
        // loggerOptions.AddConsoleExporter(); // if you need to debug the OtlpExporter

        loggerOptions.IncludeFormattedMessage = true;
        loggerOptions.IncludeScopes = true;
        loggerOptions.ParseStateValues = true;
        loggerOptions.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(ServiceName)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["host.name"] = Environment.MachineName,
                ["os.description"] = RuntimeInformation.OSDescription,
                ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
            }));
    });

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
         tracerProviderBuilder
            .AddSource(ActivitySource.Name)
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
     .WithMetrics(metricsProviderBuilder =>
        metricsProviderBuilder
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
            .AddMeter(nameof(SubscriptionManagerTest))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

builder.Services.AddTownsharp();
builder.Services.AddHostedService<SubscriptionManagerTest>();

IHost host = builder.Build();
host.Run();

internal class SubscriptionManagerTest : IHostedService
{
    public static Meter meter = new Meter(nameof(SubscriptionManagerTest));

    private readonly WebApiClient webApiClient;
    private readonly SubscriptionManagerFactory subscriptionManagerFactory;
    private readonly ILogger<SubscriptionManagerTest> logger;

    private readonly Counter<long> eventCounter;
    private long totalCount;

    private SubscriptionManager? subscriptionManager;

    public SubscriptionManagerTest(
        WebApiClient webApiClient,
        SubscriptionManagerFactory subscriptionManagerFactory,
        ILogger<SubscriptionManagerTest> logger)
    {
        this.webApiClient = webApiClient;
        this.subscriptionManagerFactory = subscriptionManagerFactory;
        this.logger = logger;
        this.eventCounter = meter.CreateCounter<long>("tsharp_ex_sm_dm_subscription_events_received");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.subscriptionManager = await this.subscriptionManagerFactory.CreateAsync();
        this.subscriptionManager.OnSubscriptionEvent += (sender, subscriptionEvent) =>
        {
            this.eventCounter.Add(1);
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