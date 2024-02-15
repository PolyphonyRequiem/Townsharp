using MediatR;

using Townsharp.Infrastructure.WebApi;
using Townsharp.Internals.Sessions.Requests;

namespace Townsharp.Internals.Servers.Handlers;

internal class AutoManageServersRequestHandler : IRequestHandler<AutoManageServersRequest>
{
    private readonly WebApiBotClient webApiClient;
    private readonly ServerManager gameServerManager;

    public AutoManageServersRequestHandler(WebApiBotClient webApiClient, ServerManager gameServerManager)
    {
        this.webApiClient = webApiClient;
        this.gameServerManager = gameServerManager;
    }

    public async Task Handle(AutoManageServersRequest request, CancellationToken cancellationToken)
    {
        await foreach (var server in webApiClient.GetJoinedServersAsyncStream())
        {
            int serverId = server.id;
            int groupId = server.group_id;

            await this.gameServerManager.ManageGameServerAsync(serverId, groupId);
        }
    }
}
