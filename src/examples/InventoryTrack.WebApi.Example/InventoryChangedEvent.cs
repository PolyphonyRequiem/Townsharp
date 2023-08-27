namespace InventoryTrack.WebApi.Example;

public abstract record InventoryChangedEvent(string Name, uint Quantity)
{
    internal static InventoryChangedEvent Create(string changeType, string inventoryType, string item, uint quantity)
    {
        if (changeType == "Dock" && inventoryType == "Player")
        {
            return new DockInventoryChangedEvent(item, quantity);
        }
        if (changeType == "Undock" && inventoryType == "Player")
        {
            return new UndockInventoryChangedEvent(item, quantity);
        }

        return new NoOpInventoryChangedEvent(item, quantity);
    }
}

public record PickupInventoryChangedEvent(string Name, uint Quantity) : InventoryChangedEvent(Name, Quantity);

public record DropInventoryChangedEvent(string Name, uint Quantity) : InventoryChangedEvent(Name, Quantity);

public record DockInventoryChangedEvent(string Name, uint Quantity) : InventoryChangedEvent(Name, Quantity);

public record UndockInventoryChangedEvent(string Name, uint Quantity) : InventoryChangedEvent(Name, Quantity);

public record NoOpInventoryChangedEvent(string Name, uint Quantity) : InventoryChangedEvent(Name, Quantity);