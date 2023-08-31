using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.ServerConsole;

namespace Townsharp.Infrastructure.GameConsole;

public class ConsoleSessionFactory
{
    private readonly ILoggerFactory loggerFactory;

    public ConsoleSessionFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public Task<ConsoleSession> CreateAndConnectAsync(Uri consoleWebsocketUri, string authToken)
    {
        return ConsoleSession.CreateAndConnectAsync(
            consoleWebsocketUri,
            authToken,
            this.loggerFactory.CreateLogger<ConsoleSession>());
    }
}
