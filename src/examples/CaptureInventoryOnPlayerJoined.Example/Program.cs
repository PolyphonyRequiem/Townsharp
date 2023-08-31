using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.Hosting;
using Townsharp.Infrastructure.ServerConsole;
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
            .AddMeter(nameof(CaptureInventoryOnJoin))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

builder.Services.AddTownsharp();
builder.Services.AddHostedService<CaptureInventoryOnJoin>();

IHost host = builder.Build();
host.Run();

public class CaptureInventoryOnJoin : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly SubscriptionManagerFactory subscriptionManagerFactory;
    private readonly ILogger<CaptureInventoryOnJoin> logger;

    public CaptureInventoryOnJoin(
        WebApiClient webApiClient,
        ConsoleClientFactory consoleClientFactory,
        SubscriptionManagerFactory subscriptionManagerFactory,
        ILogger<CaptureInventoryOnJoin> logger)
    {
        this.webApiClient = webApiClient;
        this.consoleClientFactory = consoleClientFactory;
        this.subscriptionManagerFactory = subscriptionManagerFactory;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => ListenForJoiners(cancellationToken));
    }

    private async Task ListenForJoiners(CancellationToken cancellationToken = default)
    {
        await foreach (var joinedServer in this.webApiClient.GetJoinedServersAsync())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!joinedServer["is_online"]?.GetValue<bool>() ?? false)
                    {
                        return;
                    }

                    var serverId = joinedServer["id"]?.GetValue<ulong>() ?? throw new Exception("Failed to get id from response.");
                        
                    var response = await this.webApiClient.RequestConsoleAccessAsync(serverId);

                    if (!response["allowed"]?.GetValue<bool>() ?? false)
                    {
                        throw new InvalidOperationException("Server is not online.");
                    }

                    UriBuilder uriBuilder = new UriBuilder();

                    uriBuilder.Scheme = "ws";
                    uriBuilder.Host = response["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
                    uriBuilder.Port = response["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

                    var consoleClient = await this.consoleClientFactory.CreateAndConnectAsync(
                        uriBuilder.Uri,
                        response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."));

                    await this.SubscribePlayerJoined(consoleClient);

                    consoleClient.GameConsoleEventReceived += (s, e) => this.HandleEvent(consoleClient, e);
                    consoleClient.Disconnected += (s, _) => this.logger.LogInformation("Disconnected from server {serverId}.", serverId);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to get console access a server.");
                }
            });
        }
    }

    private void HandleEvent(ConsoleClient consoleClient, GameConsoleEvent ev)
    {
        Task.Run(async () =>
        {
            try
            {
                if (ev.Result["eventType"]?.GetValue<string>() == "PlayerJoined")
                {
                    this.logger.LogInformation("EVENT: {playerJoinedEvent}", ev.Result.ToJsonString());
                }

                var playerId = ev.Result["data"]?["user"]?["id"]?.GetValue<long>() ?? throw new InvalidDataException("Failed to get user id from event.");

                await Task.Delay(TimeSpan.FromSeconds(10));
                var inventoryCommand = await consoleClient.RunCommand($"player inventory {playerId}", TimeSpan.FromSeconds(30));

                this.logger.LogInformation("Inventory Response: {inventoryResponse}", inventoryCommand.Result?.ToJsonString());

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to handle event.");
            }
        });
    }

    private async Task SubscribePlayerJoined(ConsoleClient consoleClient)
    {
        try
        {
            _ = await consoleClient.RunCommand("websocket subscribe PlayerJoined", TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to subscribe to PlayerJoined.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}