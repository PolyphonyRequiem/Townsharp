using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Townsharp;

internal class ServerGroupManager : IReadOnlyDictionary<ServerGroupId, ServerGroup>
{
    private readonly Dictionary<ServerGroupId, ServerGroup> managedServerGroups;

    public ServerGroupManager()
    {
        this.managedServerGroups = new Dictionary<ServerGroupId, ServerGroup>();
    }

    public ServerGroup this[ServerGroupId key] => this.managedServerGroups[key];

    public IEnumerable<ServerGroupId> Keys => this.managedServerGroups.Keys;

    public IEnumerable<ServerGroup> Values => this.managedServerGroups.Values;

    public int Count => this.managedServerGroups.Count;

    public bool ContainsKey(ServerGroupId key) => this.managedServerGroups.ContainsKey(key);

    public IEnumerator<KeyValuePair<ServerGroupId, ServerGroup>> GetEnumerator() => this.managedServerGroups.GetEnumerator();

    public bool TryGetValue(ServerGroupId key, [MaybeNullWhen(false)] out ServerGroup value) => this.managedServerGroups.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => this.managedServerGroups.GetEnumerator();
}