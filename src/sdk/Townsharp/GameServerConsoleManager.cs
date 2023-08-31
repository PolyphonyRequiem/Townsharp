using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Townsharp;

internal class GameServerConsoleManager : IReadOnlyDictionary<GameServerId, GameServerConsole>
{
    private readonly Dictionary<GameServerId, GameServerConsole> gameServerConsoles;

    public GameServerConsoleManager()
    {
        this.gameServerConsoles = new Dictionary<GameServerId, GameServerConsole>();
    }

    public GameServerConsole this[GameServerId key] => this.gameServerConsoles[key];

    public IEnumerable<GameServerId> Keys => this.gameServerConsoles.Keys;

    public IEnumerable<GameServerConsole> Values => this.gameServerConsoles.Values;

    public int Count => this.gameServerConsoles.Count();

    public bool ContainsKey(GameServerId key) => this.gameServerConsoles.ContainsKey(key);

    public IEnumerator<KeyValuePair<GameServerId, GameServerConsole>> GetEnumerator() => this.gameServerConsoles.GetEnumerator();

    public bool TryGetValue(GameServerId key, [MaybeNullWhen(false)] out GameServerConsole value) => this.gameServerConsoles.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => this.gameServerConsoles.GetEnumerator();
}
