using MediatR;

using Microsoft.Extensions.Logging;

using Townsharp.Configuration;
using Townsharp.Internals.Groups;
using Townsharp.Internals.Servers;
using Townsharp.Internals.Sessions.Requests;
using Townsharp.Servers;

namespace Townsharp;

public class Session
{
    private readonly IMediator mediator;
    private readonly SessionConfiguration sessionConfiguration;
    private readonly ServerManager serverManager;
    private readonly GroupManager groupManager;
    private readonly ILogger<Session> logger;
    public readonly Task initTask;

    internal Session(
        IMediator sessionMediator,
        SessionConfiguration sessionConfiguration,
        ServerManager serverManager,
        GroupManager groupManager,
        ILogger<Session> logger)
    {
        this.mediator = sessionMediator;
        this.sessionConfiguration = sessionConfiguration;
        this.serverManager = serverManager;
        this.groupManager = groupManager;
        this.serverManager = serverManager;
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

    public IReadOnlyDictionary<ServerId, Server> Servers => this.serverManager;

    public IReadOnlyDictionary<GroupId, Group> Groups => this.groupManager;
}
