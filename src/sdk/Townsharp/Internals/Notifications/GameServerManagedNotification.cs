using MediatR;
using Townsharp.Servers;

namespace Townsharp.Internals.Notifications;

internal class GameServerManagedNotification : INotification
{
    public GameServerManagedNotification(ServerId serverId)
    {
        this.ServerId = serverId;
    }

    public ServerId ServerId { get; }
}
