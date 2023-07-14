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

    public Task StartNew(
        Uri consoleWebsocketUri,
        string authToken,
        Action<ConsoleSession> onSessionConnected,
        Action<ConsoleSession, IAsyncEnumerable<GameConsoleEvent>> handleEvents,
        Action<Exception?> onDisconnected,
        CancellationToken cancellationToken = default)
    {
        return ConsoleSession.ConnectAsync(
            consoleWebsocketUri,
            authToken,
            this.loggerFactory.CreateLogger<ConsoleSession>(),
            onSessionConnected, 
            handleEvents, 
            onDisconnected, 
            cancellationToken);
    }
}
