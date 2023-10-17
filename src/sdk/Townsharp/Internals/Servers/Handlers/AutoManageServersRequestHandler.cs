using MediatR;

using Townsharp.Internals.Sessions.Requests;

namespace Townsharp.Internals.Servers.Handlers;

internal class AutoManageServersRequestHandler : IRequestHandler<AutoManageServersRequest>
{
    private readonly WebApiClient webApiClient;
    private readonly ServerManager gameServerManager;

    public AutoManageServersRequestHandler(WebApiClient webApiClient, ServerManager gameServerManager)
    {
        this.webApiClient = webApiClient;
        this.gameServerManager = gameServerManager;
    }

    public async Task Handle(AutoManageServersRequest request, CancellationToken cancellationToken)
    {
        await foreach (var server in webApiClient.GetJoinedServersAsync())
        {
            int serverId = server["id"]?.GetValue<int>() ?? throw new InvalidOperationException("Unable to parse server id from joined servers response.");
            int groupId = server["group_id"]?.GetValue<int>() ?? throw new InvalidOperationException("Unable to parse group id from joined servers response.");

            await this.gameServerManager.ManageGameServerAsync(serverId, groupId);
        }
    }
}
