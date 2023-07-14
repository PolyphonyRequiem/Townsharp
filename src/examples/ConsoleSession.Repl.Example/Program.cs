using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
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
            .AddMeter(nameof(ConsoleRepl))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

builder.Services.AddTownsharp();
builder.Services.AddHostedService<ConsoleRepl>();

IHost host = builder.Build();
host.Run();

public class ConsoleRepl : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly ConsoleSessionFactory consoleSessionFactory;
    private readonly ILogger<ConsoleRepl> logger;

    public ConsoleRepl(WebApiClient webApiClient, ConsoleSessionFactory consoleSessionFactory, ILogger<ConsoleRepl> logger)
    {
        this.webApiClient = webApiClient;
        this.consoleSessionFactory = consoleSessionFactory;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => StartConsoleReplAsync(cancellationToken));
    }

    private async Task StartConsoleReplAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Enter the server id to connect to:");
        var serverId = Console.ReadLine();

        var response = await this.webApiClient.RequestConsoleAccessAsync(long.Parse(serverId!));

        if (!response["allowed"]?.GetValue<bool>() ?? false)
        {
            throw new InvalidOperationException("Server is not online.");
        }

        UriBuilder uriBuilder = new UriBuilder();

        uriBuilder.Scheme = "ws";
        uriBuilder.Host = response["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
        uriBuilder.Port = response["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

        CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await this.consoleSessionFactory.StartNew(
            uriBuilder.Uri, 
            response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."),
            onSessionConnected: consoleSession => Task.Run(() => this.GetCommands(consoleSession, cancellationTokenSource.Token)),
            handleEvents: (consoleSession, events) => events.ForEachAsync(e => this.logger.LogInformation(e.ToString())),
            onDisconnected: exception =>
            {
                this.logger.LogInformation(exception, "Disconnected from server {serverId}.", serverId);
                cancellationTokenSource.Cancel();
            },
            cancellationToken);
    }
    private async Task GetCommands(ConsoleSession consoleSession, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Console.WriteLine("Enter a command to send to the server:");
            var command = await GetInputAsync(token);

            if (command == "exit" || String.IsNullOrEmpty(command))
            {
                break;
            }

            var result = await consoleSession.RunCommand(command!, TimeSpan.FromSeconds(30), token);

            Console.WriteLine(result.ToString());
        }
    }

    private Task<string?> GetInputAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(Console.ReadLine, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}