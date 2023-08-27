using InventoryTrack.WebApi.Example;

using MediatR;

using Townsharp;

internal class GetInventoryCommand : IRequest<IEnumerable<InventoryItem>>
{
    public GetInventoryCommand(GameServerId gameServerId, PlayerId playerId)
    {
        this.GameServerId = gameServerId;
        this.PlayerId = playerId;
    }

    public GameServerId GameServerId { get; }

    public PlayerId PlayerId { get; }
}