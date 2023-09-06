using MediatR;

using Microsoft.Extensions.Logging;

using Townsharp.Configuration;
using Townsharp.Consoles;
using Townsharp.Internals.Consoles;
using Townsharp.Internals.GameServers;
using Townsharp.Internals.ServerGroups;
using Townsharp.Internals.Sessions.Requests;

namespace Townsharp;

public class Session
{
    private readonly IMediator mediator;
    private readonly SessionConfiguration sessionConfiguration;
    private readonly GameServerManager gameServerManager;
    private readonly ServerGroupManager serverGroupManager;
    private readonly GameServerConsoleManager gameServerConsoleManager;
    private readonly ILogger<Session> logger;
    public readonly Task initTask;

    internal Session(
        IMediator sessionMediator,
        SessionConfiguration sessionConfiguration,
        GameServerManager gameServerManager,
        ServerGroupManager serverGroupManager,
        GameServerConsoleManager gameServerConsoleManager,
        ILogger<Session> logger)
    {
        this.mediator = sessionMediator;
        this.sessionConfiguration = sessionConfiguration;
        this.gameServerManager = gameServerManager;
        this.serverGroupManager = serverGroupManager;
        this.gameServerConsoleManager = gameServerConsoleManager;
        this.logger = logger;

        this.initTask = this.InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        List<Task> initTasks = new List<Task>();

        if (this.sessionConfiguration.AutoAcceptInvitations)
        {
            initTasks.Add(this.SetupAutoAcceptInvitations());
        }

        if (this.sessionConfiguration.AutoManageJoinedServers)
        {
            initTasks.Add(this.SetupAutoManageJoinedServers());
        }

        await Task.WhenAll(initTasks);
        this.logger.LogInformation("Session Ready.");
    }

    private async Task SetupAutoManageJoinedServers()
    {
        await this.mediator.Send(new AutoManageServersRequest());
    }

    private async Task SetupAutoAcceptInvitations()
    {
        await this.mediator.Send(new AutoAcceptInvitationsRequest());
    }

    public IReadOnlyDictionary<GameServerId, GameServer> Servers => this.gameServerManager;

    public IReadOnlyDictionary<ServerGroupId, ServerGroup> Groups => this.serverGroupManager;

    public IReadOnlyDictionary<GameServerId, GameServerConsole> Consoles => this.gameServerConsoleManager;
}
