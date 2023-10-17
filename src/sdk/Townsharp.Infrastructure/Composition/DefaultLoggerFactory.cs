using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Composition;

internal static class InternalLoggerFactory
{
    static InternalLoggerFactory()
    {
        ServiceCollection services = new();
        services.AddLogging(
            config =>
            {
                config.AddConsole();
            });
        
        defaultInstance = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    }

    private static ILoggerFactory defaultInstance;

    internal static ILoggerFactory Default => defaultInstance;
}
