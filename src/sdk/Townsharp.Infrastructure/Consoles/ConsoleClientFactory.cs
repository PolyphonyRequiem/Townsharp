using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.GameConsoles;

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
