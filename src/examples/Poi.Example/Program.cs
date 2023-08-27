using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Hosting;

Console.WriteLine("Starting Poi test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Logging.AddConsole();
builder.Services.AddTownsharp();
builder.Services.AddHostedService<PoiService>();

IHost host = builder.Build();
host.Run();