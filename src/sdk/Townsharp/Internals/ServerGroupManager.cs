using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

namespace Townsharp.Internals;

internal class ServerGroupManager : IReadOnlyDictionary<ServerGroupId, ServerGroup>
{
    private readonly Dictionary<ServerGroupId, ServerGroup> managedServerGroups;
    private readonly IMediator mediator;

    public ServerGroupManager(IMediator mediator)
    {
        managedServerGroups = new Dictionary<ServerGroupId, ServerGroup>();
        this.mediator = mediator;
    }

    public ServerGroup this[ServerGroupId key] => managedServerGroups[key];

    public IEnumerable<ServerGroupId> Keys => managedServerGroups.Keys;

    public IEnumerable<ServerGroup> Values => managedServerGroups.Values;

    public int Count => managedServerGroups.Count;

    public bool ContainsKey(ServerGroupId key) => managedServerGroups.ContainsKey(key);

    public IEnumerator<KeyValuePair<ServerGroupId, ServerGroup>> GetEnumerator() => managedServerGroups.GetEnumerator();

    public bool TryGetValue(ServerGroupId key, [MaybeNullWhen(false)] out ServerGroup value) => managedServerGroups.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => managedServerGroups.GetEnumerator();
}