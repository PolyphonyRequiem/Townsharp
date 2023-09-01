using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;
using Townsharp.Consoles;

namespace Townsharp.Internals;

internal class GameServerConsoleManager : IReadOnlyDictionary<GameServerId, GameServerConsole>
{
    internal readonly Dictionary<GameServerId, GameServerConsole> gameServerConsoles;
    private readonly IMediator mediator;

    public GameServerConsoleManager(IMediator mediator)
    {
        gameServerConsoles = new Dictionary<GameServerId, GameServerConsole>();
        this.mediator = mediator;
    }

    public GameServerConsole this[GameServerId key] => gameServerConsoles[key];

    public IEnumerable<GameServerId> Keys => gameServerConsoles.Keys;

    public IEnumerable<GameServerConsole> Values => gameServerConsoles.Values;

    public int Count => gameServerConsoles.Count();

    public bool ContainsKey(GameServerId key) => gameServerConsoles.ContainsKey(key);

    public IEnumerator<KeyValuePair<GameServerId, GameServerConsole>> GetEnumerator() => gameServerConsoles.GetEnumerator();

    public bool TryGetValue(GameServerId key, [MaybeNullWhen(false)] out GameServerConsole value) => gameServerConsoles.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => gameServerConsoles.GetEnumerator();
}
