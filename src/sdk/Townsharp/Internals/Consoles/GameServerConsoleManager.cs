using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

using Microsoft.Extensions.Logging;

using Townsharp.Consoles;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Servers;

namespace Townsharp.Internals.Consoles;

internal class GameServerConsoleManager : IReadOnlyDictionary<ServerId, GameServerConsole>
{
    private readonly Dictionary<ServerId, GameServerConsole> gameServerConsoles;
    private readonly IMediator mediator;
    private readonly ConsoleAccessProvider consoleAccessProvider;
    private readonly ILoggerFactory loggerFactory;

    public GameServerConsoleManager(
        IMediator mediator,
        ConsoleAccessProvider consoleAccessProvider,
        ILoggerFactory loggerFactory)
    {
        gameServerConsoles = new Dictionary<ServerId, GameServerConsole>();
        this.mediator = mediator;
        this.consoleAccessProvider = consoleAccessProvider;
        this.loggerFactory = loggerFactory;
    }

    public GameServerConsole this[ServerId key] => gameServerConsoles[key];

    public IEnumerable<ServerId> Keys => gameServerConsoles.Keys;

    public IEnumerable<GameServerConsole> Values => gameServerConsoles.Values;

    public int Count => gameServerConsoles.Count();

    public bool ContainsKey(ServerId key) => gameServerConsoles.ContainsKey(key);

    public IEnumerator<KeyValuePair<ServerId, GameServerConsole>> GetEnumerator() => gameServerConsoles.GetEnumerator();

    public bool TryGetValue(ServerId key, [MaybeNullWhen(false)] out GameServerConsole value) => gameServerConsoles.TryGetValue(key, out value);

    internal Task ManageGameServerConsoleAsync(ServerId serverId)
    {
        var console = new GameServerConsole(serverId, this.consoleClientFactory, this.consoleAccessProvider, this.loggerFactory.CreateLogger<GameServerConsole>());
        this.gameServerConsoles.Add(serverId, console);
        //console.TryToConnect();

        return Task.CompletedTask;
    }

    IEnumerator IEnumerable.GetEnumerator() => gameServerConsoles.GetEnumerator();
}
