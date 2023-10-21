using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Townsharp.Configuration;
using Townsharp.Infrastructure.Hosting;
using Townsharp.Internals.Consoles;
using Townsharp.Internals.Groups;
using Townsharp.Internals.Servers;

namespace Townsharp.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTownsharp(this IServiceCollection services)
    {
        var internalProvider = BuildInternalServiceProvider();

        Session sessionInstance = new(
                internalProvider.GetRequiredService<IMediator>(),
                new SessionConfiguration(),
                internalProvider.GetRequiredService<ServerManager>(),
                internalProvider.GetRequiredService<GroupManager>(),
                internalProvider.GetRequiredService<ILogger<Session>>());

        services.AddSingleton(sessionInstance);

        return services;
    }

    private static IServiceProvider BuildInternalServiceProvider()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        });

        services.AddTownsharpInfra();

        services.AddSingleton<ServerManager>();
        services.AddSingleton<GroupManager>();
        services.AddSingleton<GameServerConsoleManager>();
        services.AddSingleton<ConsoleAccessProvider>();

        return services.BuildServiceProvider();
    }

}
