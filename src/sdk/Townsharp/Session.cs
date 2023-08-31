using MediatR;

using Townsharp.Infrastructure.Identity;

namespace Townsharp;

public class Session
{
    private readonly Mediator sessionMediator;
    private readonly GameServerManager gameServerManager;
    private readonly ServerGroupManager serverGroupManager;
    private readonly GameServerConsoleManager gameServerConsoleManager;

    internal Session(
        Mediator sessionMediator, 
        GameServerManager gameServerManager,
        ServerGroupManager serverGroupManager,
        GameServerConsoleManager gameServerConsoleManager)
    {
        this.sessionMediator = sessionMediator;
        this.gameServerManager = gameServerManager;
        this.serverGroupManager = serverGroupManager;
        this.gameServerConsoleManager = gameServerConsoleManager;
    }


    public IReadOnlyDictionary<GameServerId, GameServer> Servers => this.gameServerManager;

    public IReadOnlyDictionary<ServerGroupId, ServerGroup> Groups => this.serverGroupManager;

    public IReadOnlyDictionary<GameServerId, GameServerConsole> Consoles => this.gameServerConsoleManager;
}
