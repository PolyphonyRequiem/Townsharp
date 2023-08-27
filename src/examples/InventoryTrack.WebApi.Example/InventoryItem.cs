namespace InventoryTrack.WebApi.Example;

public class InventoryItem
{
    internal static readonly IEqualityComparer<InventoryItem> Comparer = new InventoryItemComparer();
    public static InventoryItem None = new InventoryItem("None", 0);

    public InventoryItem(string name, uint nestedQuantity)
    {
        this.Name = name;
        this.NestedQuantity = nestedQuantity;
    }

    public string Name { get; }

    public uint NestedQuantity { get; }
}