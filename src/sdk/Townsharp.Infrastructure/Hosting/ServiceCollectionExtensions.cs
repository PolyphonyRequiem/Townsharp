using Microsoft.Extensions.DependencyInjection;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Identity.Models;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTownsharpInfra(this IServiceCollection services)
    {
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

        return services;
    }
}
