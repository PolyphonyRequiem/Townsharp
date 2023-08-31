using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.ServerConsole;

namespace Townsharp.Infrastructure.GameConsole;

public class ConsoleClientFactory
{
    private readonly ILoggerFactory loggerFactory;

    public ConsoleClientFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public Task<ConsoleClient> CreateAndConnectAsync(Uri consoleWebsocketUri, string authToken)
    {
        return ConsoleClient.CreateAndConnectAsync(
            consoleWebsocketUri,
            authToken,
            this.loggerFactory.CreateLogger<ConsoleClient>());
    }
}
