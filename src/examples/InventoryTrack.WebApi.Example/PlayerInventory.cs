using System.Collections.Immutable;

namespace InventoryTrack.WebApi.Example;

public class PlayerInventory
{
    private readonly PlayerId playerId;
    private readonly string playerName;

    public ImmutableList<InventoryItem> inventoryState = ImmutableList<InventoryItem>.Empty;    
    public ImmutableQueue<InventoryChangedEvent> unresolvedInventoryChangeEvents = ImmutableQueue<InventoryChangedEvent>.Empty;

    public PlayerId PlayerId => this.playerId;

    public string PlayerName => this.playerName;

    public PlayerInventory(PlayerId playerId, string playerName)
    {
        this.playerId = playerId;
        this.playerName = playerName;
    }

    public void AddInventoryChangeEvent(InventoryChangedEvent inventoryChangeEvent)
    {
        ImmutableInterlocked.Enqueue(ref this.unresolvedInventoryChangeEvents, inventoryChangeEvent);
    }

    public IEnumerable<InventoryItem> GetInventory()
    {
        ProcessAllInventoryChangedEvents();
        return this.inventoryState.AsEnumerable();
    }

    private void ProcessAllInventoryChangedEvents()
    {
        var eventsToProcess = DequeueAllEvents();
        ApplyAllChangesAndUpdateInventory(eventsToProcess);
    }


    private ImmutableQueue<InventoryChangedEvent> DequeueAllEvents()
    {
        ImmutableQueue<InventoryChangedEvent> localEvents = ImmutableQueue<InventoryChangedEvent>.Empty;

        // Try to atomically dequeue all events
        ImmutableInterlocked.Update(ref unresolvedInventoryChangeEvents, originalEvents =>
        {
            localEvents = originalEvents;
            return ImmutableQueue<InventoryChangedEvent>.Empty;  // We clear the original queue.
        });

        return localEvents;
    }

    private void ApplyAllChangesAndUpdateInventory(ImmutableQueue<InventoryChangedEvent> events)
    {
        var localEvents = events;

        // Use ImmutableInterlocked.Update to ensure atomic updates
        ImmutableInterlocked.Update(ref inventoryState, originalInventory =>
        {
            var localInventory = originalInventory;

            // Process all events locally
            while (!localEvents.IsEmpty)
            {
                localEvents = localEvents.Dequeue(out var currentEvent);
                localInventory = ApplyChange(localInventory, currentEvent);
            }

            return localInventory;
        });
    }

    private ImmutableList<InventoryItem> ApplyChange(ImmutableList<InventoryItem> localInventory, InventoryChangedEvent currentEvent)
    {
        var oldInv = localInventory;
        var newInv = currentEvent switch
        {
            //PickupInventoryChangedEvent pickupInventoryChangedEvent => localInventory.Add(new InventoryItem(pickupInventoryChangedEvent.Name, pickupInventoryChangedEvent.Quantity)),
            //DropInventoryChangedEvent dropInventoryChangedEvent => localInventory.Remove(new InventoryItem(dropInventoryChangedEvent.Name, dropInventoryChangedEvent.Quantity)),
            DockInventoryChangedEvent dockInventoryChangedEvent => localInventory.Add(new InventoryItem(dockInventoryChangedEvent.Name, dockInventoryChangedEvent.Quantity)),
            UndockInventoryChangedEvent unDockInventoryChangedEvent => localInventory.Remove(new InventoryItem(unDockInventoryChangedEvent.Name, unDockInventoryChangedEvent.Quantity), InventoryItem.Comparer),
            _ => localInventory
        };

        if (currentEvent is not NoOpInventoryChangedEvent)
        {
            Console.WriteLine($"{currentEvent.GetType().Name}:{currentEvent.Name}:{currentEvent.Quantity} | old:{oldInv.Count} new:{newInv.Count}");
        }

        return newInv;
    }
}
