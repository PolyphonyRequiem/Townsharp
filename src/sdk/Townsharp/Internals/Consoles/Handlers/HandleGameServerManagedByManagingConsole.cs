using MediatR;

using Townsharp.Internals.Notifications;

namespace Townsharp.Internals.Consoles.Handlers;

internal class HandleGameServerManagedByManagingConsole : INotificationHandler<GameServerManagedNotification>
{
    private readonly GameServerConsoleManager consoleManager;

    public HandleGameServerManagedByManagingConsole(GameServerConsoleManager consoleManager)
    {
        this.consoleManager = consoleManager;
    }

    public Task Handle(GameServerManagedNotification notification, CancellationToken cancellationToken)
    {
        return this.consoleManager.ManageGameServerConsoleAsync(notification.ServerId);
    }
}
