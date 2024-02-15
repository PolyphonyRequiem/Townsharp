using Microsoft.Extensions.DependencyInjection;

namespace Townsharp.Infrastructure.Composition;

internal static class InternalHttpClientFactory
{
    static InternalHttpClientFactory()
    {
        ServiceCollection services = new();
        services.AddHttpClient();
        defaultInstance = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private static IHttpClientFactory defaultInstance;

    internal static IHttpClientFactory Default => defaultInstance;
}
