using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

using Townsharp.Internals.Notifications;
using Townsharp.Servers;

namespace Townsharp.Internals.Servers;

internal class ServerManager : IReadOnlyDictionary<ServerId, Server>
{
    // Are these actually GameServer-Contexts-?  Perhaps.  Either way, we need to expose the lkg state clearly as LKG, and -live- state as a snapshot of some sort.
    // This about this type (the manager) in isolation as much as possible.
    private readonly Dictionary<ServerId, Server> managedGameServers;
    private readonly IMediator mediator;

    public ServerManager(IMediator mediator)
    {
        managedGameServers = new Dictionary<ServerId, Server>();
        this.mediator = mediator;
    }

    // IReadonlyDictionary Implementation
    public Server this[ServerId key] => managedGameServers[key];

    public IEnumerable<ServerId> Keys => managedGameServers.Keys;

    public IEnumerable<Server> Values => managedGameServers.Values;

    public int Count => managedGameServers.Count;

    public bool ContainsKey(ServerId key) => managedGameServers.ContainsKey(key);

    public IEnumerator<KeyValuePair<ServerId, Server>> GetEnumerator() => managedGameServers.GetEnumerator();

    public bool TryGetValue(ServerId key, [MaybeNullWhen(false)] out Server value) => managedGameServers.TryGetValue(key, out value);

    internal async Task ManageGameServerAsync(ServerId serverId, GroupId groupId)
    {
        throw new NotImplementedException();
        // this.managedGameServers.Add(serverId, new Server(serverId, groupId));
        // await mediator.Publish(new GameServerManagedNotification(serverId));
    }

    IEnumerator IEnumerable.GetEnumerator() => managedGameServers.GetEnumerator();
}