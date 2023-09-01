using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Internals;

internal class ConsoleAccessProvider
{
    private readonly WebApiClient webApiClient;
    private readonly ILogger<ConsoleAccessProvider> logger;

    public ConsoleAccessProvider(WebApiClient webApiClient, ILogger<ConsoleAccessProvider> logger)
    {
        this.webApiClient = webApiClient;
        this.logger = logger;
    }

    public async Task<ConsoleAccess> GetConsoleAccess(GameServerId serverId)
    {
        try
        {
            var response = await this.webApiClient.RequestConsoleAccessAsync(serverId);
            if (!response["allowed"]?.GetValue<bool>() ?? false)
            {
                this.logger.LogTrace($"Unable to get access for server {serverId}.  Access was not granted.");
                return ConsoleAccess.None;
            }

            UriBuilder uriBuilder = new UriBuilder();

            uriBuilder.Scheme = "ws";

            string? hostAddress = response["connection"]?["address"]?.GetValue<string>();
            if (hostAddress == default)
            {
                this.logger.LogTrace($"Failed to get connection.address from response. Access not currently available for {serverId}");
                return ConsoleAccess.None;
            }
            uriBuilder.Host = hostAddress!;

            int? post = response["connection"]?["websocket_port"]?.GetValue<int>();
            if (post == default)
            {
                this.logger.LogTrace($"Failed to get connection.host from response. Access not currently available for {serverId}");
                return ConsoleAccess.None;
            }
            uriBuilder.Port = post.GetValueOrDefault();

            return new ConsoleAccess(uriBuilder.Uri, response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."));
        }
        catch (Exception ex) 
        {
            this.logger.LogError(ex, $"An error occurred while attempting to get access for {serverId}");
            return ConsoleAccess.None;
        }
    }
}
