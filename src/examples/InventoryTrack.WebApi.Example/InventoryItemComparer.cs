using System.Diagnostics.CodeAnalysis;

namespace InventoryTrack.WebApi.Example;

internal class InventoryItemComparer : IEqualityComparer<InventoryItem>
{
    public bool Equals(InventoryItem? x, InventoryItem? y)
    {
        return x?.Name == y?.Name && x?.NestedQuantity == y?.NestedQuantity;
    }

    public int GetHashCode([DisallowNull] InventoryItem obj)
    {
        return obj.Name.GetHashCode() ^ obj.NestedQuantity.GetHashCode();
    }
}