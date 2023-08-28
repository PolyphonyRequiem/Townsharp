using Townsharp.Infrastructure.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Services.AddTownsharp()
                .AddHostedService<EventBotService>();

var applicationHost = builder.Build();
applicationHost.Run();