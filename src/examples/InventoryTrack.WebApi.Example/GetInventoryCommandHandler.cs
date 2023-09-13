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
        if (!this.inventoryTracker.PlayerInventories.ContainsKey(request.ServerId))
        {
            return Task.FromResult(Enumerable.Empty<InventoryItem>());
        }

        if (!this.inventoryTracker.PlayerInventories[request.ServerId].ContainsKey(request.PlayerId))
        {
            return Task.FromResult(Enumerable.Empty<InventoryItem>());
        }

        return Task.FromResult(this.inventoryTracker.PlayerInventories[request.ServerId][request.PlayerId].GetInventory());
    }
}
