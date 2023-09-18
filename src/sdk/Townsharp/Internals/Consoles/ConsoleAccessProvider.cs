using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.WebApi;
using Townsharp.Servers;

namespace Townsharp.Internals.Consoles;

internal class ConsoleAccessProvider
{
    private readonly WebApiClient webApiClient;
    private readonly IBotTokenProvider botTokenProvider;
    private readonly ILogger<ConsoleAccessProvider> logger;

    public ConsoleAccessProvider(
        WebApiClient webApiClient, 
        IBotTokenProvider botTokenProvider, // don't like this, but it is what it is.  Probably need to bind identity throughout the system.
        ILogger<ConsoleAccessProvider> logger)
    {
        this.webApiClient = webApiClient;
        this.botTokenProvider = botTokenProvider;
        this.logger = logger;
    }

    public async Task<ConsoleAccess> GetConsoleAccessAsync(ServerId serverId)
    {
        try
        {
            if (await this.VerifyAccessIsExpectedAsync(serverId))
            {
                this.logger.LogWarning($"No access expected for {serverId}");
                return await this.RequestAndBuildGetConsoleAccessAsync(serverId);
            }
            else
            {
                return ConsoleAccess.None;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"An error occurred while attempting to get access for {serverId}");
            return ConsoleAccess.None;
        }
    }

    private async Task<bool> VerifyAccessIsExpectedAsync(ServerId serverId)
    {
        var serverResponse = await this.webApiClient.GetServerAsync(serverId);

        int groupId = serverResponse["group_id"]?.GetValue<int>() ?? throw new InvalidOperationException("Unable to get group_id from the server response.");
        bool isOnline = serverResponse["is_online"]?.GetValue<bool>() ?? throw new InvalidOperationException("Unable to get is_online from the server response.");

        if (!isOnline)
        {
            return false;
        }
        else
        {
            var botUserId = await this.botTokenProvider.GetBotUserIdAsync();
            var groupMemberResponse = await webApiClient.GetGroupMemberAsync(groupId, (int)botUserId);

            var permissions = groupMemberResponse["permissions"]?.GetValue<string>() ?? throw new InvalidOperationException($"Unable to get permissions for user {botUserId} from group {groupId}.");

            return permissions.Contains("Owner") || permissions.Contains("Moderator");
        }

    }


    private async Task<ConsoleAccess> RequestAndBuildGetConsoleAccessAsync(ServerId serverId)
    {
        var response = await webApiClient.RequestConsoleAccessAsync(serverId);
        if (!response["allowed"]?.GetValue<bool>() ?? false)
        {
            logger.LogTrace($"Unable to get access for server {serverId}.  Access was not granted.");
            return ConsoleAccess.None;
        }

        return BuildConsoleAccessFromResponseJsonObject(serverId, response);
    }

    private ConsoleAccess BuildConsoleAccessFromResponseJsonObject(ServerId serverId, JsonObject response)
    {
        UriBuilder uriBuilder = new UriBuilder();

        uriBuilder.Scheme = "ws";

        string? hostAddress = response["connection"]?["address"]?.GetValue<string>();
        if (hostAddress == default)
        {
            logger.LogTrace($"Failed to get connection.address from response. Access not currently available for {serverId}");
            return ConsoleAccess.None;
        }
        uriBuilder.Host = hostAddress!;

        int? post = response["connection"]?["websocket_port"]?.GetValue<int>();
        if (post == default)
        {
            logger.LogTrace($"Failed to get connection.host from response. Access not currently available for {serverId}");
            return ConsoleAccess.None;
        }
        uriBuilder.Port = post.GetValueOrDefault();

        return new ConsoleAccess(uriBuilder.Uri, response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."));
    }
}
