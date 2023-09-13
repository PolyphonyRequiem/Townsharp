using System.Collections;
using System.Diagnostics.CodeAnalysis;

using MediatR;

namespace Townsharp.Internals.Groups;

internal class GroupManager : IReadOnlyDictionary<GroupId, Group>
{
    private readonly Dictionary<GroupId, Group> managedServerGroups;
    private readonly IMediator mediator;

    public GroupManager(IMediator mediator)
    {
        managedServerGroups = new Dictionary<GroupId, Group>();
        this.mediator = mediator;
    }

    public Group this[GroupId key] => managedServerGroups[key];

    public IEnumerable<GroupId> Keys => managedServerGroups.Keys;

    public IEnumerable<Group> Values => managedServerGroups.Values;

    public int Count => managedServerGroups.Count;

    public bool ContainsKey(GroupId key) => managedServerGroups.ContainsKey(key);

    public IEnumerator<KeyValuePair<GroupId, Group>> GetEnumerator() => managedServerGroups.GetEnumerator();

    public bool TryGetValue(GroupId key, [MaybeNullWhen(false)] out Group value) => managedServerGroups.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => managedServerGroups.GetEnumerator();
}