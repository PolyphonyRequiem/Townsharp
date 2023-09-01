using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

namespace Townsharp.Internals;

internal class GameServerManager : IReadOnlyDictionary<GameServerId, GameServer>
{
    // Are these actually GameServer-Contexts-?  Perhaps.  Either way, we need to expose the lkg state clearly as LKG, and -live- state as a snapshot of some sort.
    // This about this type (the manager) in isolation as much as possible.
    private readonly Dictionary<GameServerId, GameServer> managedGameServers;
    private readonly IMediator mediator;

    public GameServerManager(IMediator mediator)
    {
        managedGameServers = new Dictionary<GameServerId, GameServer>();
        this.mediator = mediator;
    }

    // IReadonlyDictionary Implementation
    public GameServer this[GameServerId key] => managedGameServers[key];

    public IEnumerable<GameServerId> Keys => managedGameServers.Keys;

    public IEnumerable<GameServer> Values => managedGameServers.Values;

    public int Count => managedGameServers.Count;

    public bool ContainsKey(GameServerId key) => managedGameServers.ContainsKey(key);

    public IEnumerator<KeyValuePair<GameServerId, GameServer>> GetEnumerator() => managedGameServers.GetEnumerator();

    public bool TryGetValue(GameServerId key, [MaybeNullWhen(false)] out GameServer value) => managedGameServers.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => managedGameServers.GetEnumerator();
}