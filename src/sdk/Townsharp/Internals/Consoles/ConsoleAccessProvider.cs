using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.WebApi;
using Townsharp.Servers;

namespace Townsharp.Internals.Consoles;

internal class ConsoleAccessProvider
{
    private readonly WebApiBotClient webApiClient;
    private readonly ILogger<ConsoleAccessProvider> logger;

    public ConsoleAccessProvider(
        WebApiBotClient webApiClient, 
        ILogger<ConsoleAccessProvider> logger)
    {
        this.webApiClient = webApiClient;
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

        if (!serverResponse.IsSuccess)
        {
            return false;
        }

        int groupId = serverResponse.Content["group_id"]?.GetValue<int>() ?? throw new InvalidOperationException("Unable to get group_id from the server response.");
        bool isOnline = serverResponse.Content["is_online"]?.GetValue<bool>() ?? throw new InvalidOperationException("Unable to get is_online from the server response.");      

        if (!isOnline)
        {
            return false;
        }
        else
        {
            var botUserInfo = await this.webApiClient.GetBotUserInfoAsync();
            var groupMemberResponse = await webApiClient.GetGroupMemberAsync(groupId, botUserInfo.id);

            if (!groupMemberResponse.IsSuccess)
            {
                return false;
            }

            var permissions = groupMemberResponse.Content["permissions"]?.GetValue<string>() ?? throw new InvalidOperationException($"Unable to get permissions for user {botUserInfo.id} from group {groupId}.");

            return permissions.Contains("Owner") || permissions.Contains("Moderator");
        }

    }

    private async Task<ConsoleAccess> RequestAndBuildGetConsoleAccessAsync(ServerId serverId)
    {
        var response = await webApiClient.RequestConsoleAccessAsync(serverId);

        if (!response.IsSuccess)
        { 
            logger.LogTrace($"Unable to get access for server {serverId}.  Access was not granted.");
            return ConsoleAccess.None;
        }

        return new ConsoleAccess(response.Content.BuildConsoleUri(), response.Content.token!);
    }
}
