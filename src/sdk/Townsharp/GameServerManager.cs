using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Townsharp;

internal class GameServerManager : IReadOnlyDictionary<GameServerId, GameServer>
{
    // Are these actually GameServer-Contexts-?  Perhaps.  Either way, we need to expose the lkg state clearly as LKG, and -live- state as a snapshot of some sort.
    // This about this type (the manager) in isolation as much as possible.
    private readonly Dictionary<GameServerId, GameServer> managedGameServers;

    public GameServerManager()
    {
        this.managedGameServers = new Dictionary<GameServerId, GameServer>();
    }

    // IReadonlyDictionary Implementation
    public GameServer this[GameServerId key] => this.managedGameServers[key];

    public IEnumerable<GameServerId> Keys => this.managedGameServers.Keys;

    public IEnumerable<GameServer> Values => this.managedGameServers.Values;

    public int Count => this.managedGameServers.Count;

    public bool ContainsKey(GameServerId key) => this.managedGameServers.ContainsKey(key);

    public IEnumerator<KeyValuePair<GameServerId, GameServer>> GetEnumerator() => this.managedGameServers.GetEnumerator();

    public bool TryGetValue(GameServerId key, [MaybeNullWhen(false)] out GameServer value) => this.managedGameServers.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => this.managedGameServers.GetEnumerator();
}