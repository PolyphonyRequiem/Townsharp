using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Townsharp.Hosting;

string ServiceName = Assembly.GetExecutingAssembly().GetName().Name ?? "Unknown Assembly";
ActivitySource ActivitySource = new ActivitySource(ServiceName);

Console.WriteLine("Starting a CaveAnnouncer test.");

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
            .AddMeter(nameof(CaveAnnouncerHostedService))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

builder.Services.AddTownsharp();
builder.Services.AddHostedService<CaveAnnouncerHostedService>();

IHost host = builder.Build();
host.Run();