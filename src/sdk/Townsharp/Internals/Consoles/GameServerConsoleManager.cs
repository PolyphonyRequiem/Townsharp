using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

using Microsoft.Extensions.Logging;

using Townsharp.Consoles;
using Townsharp.Infrastructure.GameConsole;

namespace Townsharp.Internals.Consoles;

internal class GameServerConsoleManager : IReadOnlyDictionary<GameServerId, GameServerConsole>
{
    private readonly Dictionary<GameServerId, GameServerConsole> gameServerConsoles;
    private readonly IMediator mediator;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly ConsoleAccessProvider consoleAccessProvider;
    private readonly ILoggerFactory loggerFactory;

    public GameServerConsoleManager(
        IMediator mediator, 
        ConsoleClientFactory consoleClientFactory, 
        ConsoleAccessProvider consoleAccessProvider, 
        ILoggerFactory loggerFactory)
    {
        gameServerConsoles = new Dictionary<GameServerId, GameServerConsole>();
        this.mediator = mediator;
        this.consoleClientFactory = consoleClientFactory;
        this.consoleAccessProvider = consoleAccessProvider;
        this.loggerFactory = loggerFactory;
    }

    public GameServerConsole this[GameServerId key] => gameServerConsoles[key];

    public IEnumerable<GameServerId> Keys => gameServerConsoles.Keys;

    public IEnumerable<GameServerConsole> Values => gameServerConsoles.Values;

    public int Count => gameServerConsoles.Count();

    public bool ContainsKey(GameServerId key) => gameServerConsoles.ContainsKey(key);

    public IEnumerator<KeyValuePair<GameServerId, GameServerConsole>> GetEnumerator() => gameServerConsoles.GetEnumerator();

    public bool TryGetValue(GameServerId key, [MaybeNullWhen(false)] out GameServerConsole value) => gameServerConsoles.TryGetValue(key, out value);

    internal Task ManageGameServerConsoleAsync(GameServerId serverId)
    {
        var console = new GameServerConsole(serverId, this.consoleClientFactory, this.consoleAccessProvider, this.loggerFactory.CreateLogger<GameServerConsole>());
        console.TryToConnect();
        this.gameServerConsoles.Add(serverId, console);
        //await mediator.Publish(new GameServerManagedNotification(serverId));
        return Task.CompletedTask;
    }

    IEnumerator IEnumerable.GetEnumerator() => gameServerConsoles.GetEnumerator();
}
