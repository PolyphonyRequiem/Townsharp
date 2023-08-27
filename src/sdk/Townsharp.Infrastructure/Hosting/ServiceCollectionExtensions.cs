using Microsoft.Extensions.DependencyInjection;

using Townsharp.Identity;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Identity.Models;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTownsharp(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton(
            services => new BotTokenProvider(
                   new BotCredential(
                       Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID")!,
                       Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET")!),
                   services.GetRequiredService<HttpClient>()));

        services.AddSingleton(
            services => new UserTokenProvider(
                   new UserCredential(
                       Environment.GetEnvironmentVariable("TOWNSHARP_USERNAME")!,
                       Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORDHASH")!),
                   services.GetRequiredService<HttpClient>()));

        services.AddSingleton<WebApiClient>();
        services.AddSingleton<SubscriptionClientFactory>();
        services.AddSingleton<SubscriptionManagerFactory>();
        services.AddSingleton<ConsoleSessionFactory>();

        return services;
    }
}
