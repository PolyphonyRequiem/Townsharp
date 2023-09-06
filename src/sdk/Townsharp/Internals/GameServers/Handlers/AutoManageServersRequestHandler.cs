using MediatR;

using Townsharp.Infrastructure.WebApi;
using Townsharp.Internals.Sessions.Requests;

namespace Townsharp.Internals.GameServers.Handlers;

internal class AutoManageServersRequestHandler : IRequestHandler<AutoManageServersRequest>
{
    private readonly WebApiClient webApiClient;
    private readonly GameServerManager gameServerManager;

    public AutoManageServersRequestHandler(WebApiClient webApiClient, GameServerManager gameServerManager)
    {
        this.webApiClient = webApiClient;
        this.gameServerManager = gameServerManager;
    }

    public async Task Handle(AutoManageServersRequest request, CancellationToken cancellationToken)
    {
        await foreach (var server in webApiClient.GetJoinedServersAsync())
        {
            ulong serverId = server["id"]?.GetValue<ulong>() ?? throw new InvalidOperationException("Unable to parse server id from joined servers response.");
            ulong groupId = server["group_id"]?.GetValue<ulong>() ?? throw new InvalidOperationException("Unable to parse group id from joined servers response.");

            gameServerManager.ManageGameServerAsync(serverId, groupId);
        }
    }
}
