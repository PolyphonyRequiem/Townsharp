using InventoryTrack.WebApi.Example;

using MediatR;
using Townsharp;

internal class GetInventoryCommand : IRequest<IEnumerable<InventoryItem>>
{
    public GetInventoryCommand(ServerId serverId, PlayerId playerId)
    {
        this.ServerId = serverId;
        this.PlayerId = playerId;
    }

    public ServerId ServerId { get; }

    public PlayerId PlayerId { get; }
}