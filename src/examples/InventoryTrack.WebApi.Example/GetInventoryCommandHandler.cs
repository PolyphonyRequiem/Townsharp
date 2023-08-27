using MediatR;

namespace InventoryTrack.WebApi.Example;

public class GetInventoryCommandHandler : IRequestHandler<GetInventoryCommand, IEnumerable<InventoryItem>>
{
    private readonly InventoryTracker inventoryTracker;

    public GetInventoryCommandHandler(InventoryTracker inventoryTracker)
    {
        this.inventoryTracker = inventoryTracker;
    }

    Task<IEnumerable<InventoryItem>> IRequestHandler<GetInventoryCommand, IEnumerable<InventoryItem>>.Handle(GetInventoryCommand request, CancellationToken cancellationToken)
    {
        if (!this.inventoryTracker.PlayerInventories.ContainsKey(request.GameServerId))
        {
            return Task.FromResult(Enumerable.Empty<InventoryItem>());
        }

        if (!this.inventoryTracker.PlayerInventories[request.GameServerId].ContainsKey(request.PlayerId))
        {
            return Task.FromResult(Enumerable.Empty<InventoryItem>());
        }

        return Task.FromResult(this.inventoryTracker.PlayerInventories[request.GameServerId][request.PlayerId].GetInventory());
    }
}
