using System.Collections.Concurrent;
using Townsharp;

namespace InventoryTrack.WebApi.Example;

public class InventoryTracker
{
    private readonly ConcurrentDictionary<GameServerId, ConcurrentDictionary<PlayerId, PlayerInventory>> playerInventories = new ConcurrentDictionary<GameServerId, ConcurrentDictionary<PlayerId, PlayerInventory>>();

    public ConcurrentDictionary<GameServerId, ConcurrentDictionary<PlayerId, PlayerInventory>> PlayerInventories => this.playerInventories;

    public void TrackInventoryEvent(GameServerId serverId, PlayerId playerId, string userName, InventoryChangedEvent changeEvent)
    {
        if (!this.PlayerInventories.ContainsKey(serverId))
        {
            this.PlayerInventories.TryAdd(serverId, new ConcurrentDictionary<PlayerId, PlayerInventory>());
        }

        if (!this.PlayerInventories[serverId].ContainsKey(playerId))
        {
            this.PlayerInventories[serverId].TryAdd(playerId, new PlayerInventory(playerId, userName));
        }

        this.PlayerInventories[serverId][playerId].AddInventoryChangeEvent(changeEvent);
    }    
}
