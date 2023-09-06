using MediatR;

namespace Townsharp.Internals.Notifications;

internal class GameServerManagedNotification : INotification
{
    public GameServerManagedNotification(GameServerId serverId)
    {
        this.ServerId = serverId;
    }

    public GameServerId ServerId { get; }
}
