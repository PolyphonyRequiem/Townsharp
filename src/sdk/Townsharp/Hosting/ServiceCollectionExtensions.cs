using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Townsharp.Configuration;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;
using Townsharp.Internals.Consoles;
using Townsharp.Internals.Groups;
using Townsharp.Internals.Servers;

namespace Townsharp.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTownsharp(this IServiceCollection services)
    {
        var internalProvider = BuildInternalServiceProvider();

        Session sessionInstance = new Session(
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

        services.AddHttpClient();
        services.AddSingleton<IBotTokenProvider>(
            services => new BotTokenProvider(
                   new BotCredential(
                       Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID")!,
                       Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET")!),
                   services.GetRequiredService<HttpClient>()));

        var userCredential = new UserCredential(
            Environment.GetEnvironmentVariable("TOWNSHARP_USERNAME") ?? "",
            Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORDHASH") ?? "");

        if (userCredential.IsConfigured)
        {
            services.AddSingleton<IUserTokenProvider>(
            services => new UserTokenProvider(
                userCredential,
                services.GetRequiredService<HttpClient>()));
        }
        else
        {
            services.AddSingleton<IUserTokenProvider>(new DisabledUserTokenProvider());
        }

        services.AddSingleton<WebApiClient>();
        services.AddSingleton<SubscriptionClientFactory>();
        services.AddSingleton<SubscriptionMultiplexerFactory>();
        services.AddSingleton<ConsoleClientFactory>();
        services.AddSingleton<ServerManager>();
        services.AddSingleton<GroupManager>();
        services.AddSingleton<GameServerConsoleManager>();
        services.AddSingleton<ConsoleAccessProvider>();

        return services.BuildServiceProvider();
    }

}
