using Microsoft.Extensions.DependencyInjection;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.GameConsoles;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure.Hosting;

public static class ServiceCollectionExtensions
{
    // This needs to die off, either we expose standalone clients via a dedicated library, via this library, or we just support it via townsharp.
    public static IServiceCollection AddTownsharpInfra(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<BotTokenProvider>(
            services => new BotTokenProvider(
                BotCredential.FromEnvironmentVariables(),
                services.GetRequiredService<IHttpClientFactory>()));
              
        services.AddSingleton<WebApiBotClient>();
        services.AddSingleton<SubscriptionClientFactory>();
        services.AddSingleton<SubscriptionMultiplexerFactory>();
        services.AddSingleton<ConsoleClientFactory>();

        return services;
    }
}
