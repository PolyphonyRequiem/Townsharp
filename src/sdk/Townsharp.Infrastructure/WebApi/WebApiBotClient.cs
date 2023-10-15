using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiBotClient
{
    public const int PaginationLimit = 500;
    public const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly IBotTokenProvider botTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiBotClient> logger;

    public WebApiBotClient(IBotTokenProvider botTokenProvider, IHttpClientFactory httpClientFactory, ILogger<WebApiBotClient> logger)
    {
        this.botTokenProvider = botTokenProvider;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    private async Task<string> GetTokenAsync([CallerMemberName] string? callerMemberName = default) 
        => this.botTokenProvider.IsEnabled ?
            await this.botTokenProvider.GetTokenAsync() :
            throw new InvalidOperationException($"Caller {callerMemberName ?? ""} requires a configured BotTokenProvider.  Please verify that you have configured your bot token credentials correctly.");

    private async Task<HttpClient> GetClientAsync()
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(WebApiClient.BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await this.GetTokenAsync());
        return httpClient;
    }

    public async Task<JsonObject> GetGroupAsync(int groupId)
    {
        var client = await GetClientAsync();
        var response = await client.GetFromJsonAsync<JsonObject>($"api/groups/{groupId}");

        if (response == null)
        {
            throw new InvalidOperationException($"Failed to get group {groupId}.");
        }
        else
        {
            return response;
        }
    }

    public async IAsyncEnumerable<JsonObject> GetJoinedGroupsAsync()
    {
        var client = await GetClientAsync();
        HttpResponseMessage response;
        string lastPaginationToken = string.Empty;

        do
        {
            var message = lastPaginationToken != string.Empty ?
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/joined?limit={PaginationLimit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/joined?limit={PaginationLimit}");

            response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
                throw new InvalidOperationException(errorResponse.GetRawText());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedGroup in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return joinedGroup?.AsObject() ?? new JsonObject();
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    public async IAsyncEnumerable<JsonObject> GetPendingGroupInvitationsAsync()
    {
        var client = await GetClientAsync();
        HttpResponseMessage response;
        string lastPaginationToken = string.Empty;

        do
        {
            var message = lastPaginationToken != string.Empty ?
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/invites?limit={PaginationLimit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/invites?limit={PaginationLimit}");

            response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
                throw new InvalidOperationException(errorResponse.GetRawText());
            }

            if (response.Headers.Contains("paginationToken"))
            {
                lastPaginationToken = response.Headers.GetValues("paginationToken").First();
            }

            foreach (var pendingInvite in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return pendingInvite?.AsObject() ?? new JsonObject();
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    // throws 400 if the invite has already been accepted
    public async Task<bool> AcceptGroupInviteAsync(int groupId)
    {
        var client = await GetClientAsync();
        var response = await client.PostAsync($"api/groups/invites/{groupId}", new StringContent(groupId.ToString()));
        return response.IsSuccessStatusCode;
    }

    public async Task<JsonObject> GetGroupMemberAsync(int groupId, int userId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/groups/{groupId}/members/{userId}");

        if (!response.IsSuccessStatusCode)
        {
            JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            throw new InvalidOperationException(errorResponse.GetRawText());
        }

        var groupMember = await response.Content.ReadFromJsonAsync<JsonObject>();

        return groupMember!;
    }

    public async IAsyncEnumerable<JsonObject> GetJoinedServersAsync()
    {
        var client = await GetClientAsync();
        HttpResponseMessage response;
        string lastPaginationToken = string.Empty;

        do
        {
            var message = lastPaginationToken != string.Empty ?
                new HttpRequestMessage(HttpMethod.Get, $"api/servers/joined?limit={PaginationLimit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/servers/joined?limit={PaginationLimit}");

            response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
                throw new InvalidOperationException(errorResponse.GetRawText());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedServer in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return joinedServer?.AsObject() ?? new JsonObject();
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    public async Task<JsonObject> GetServerAsync(int serverId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/servers/{serverId}");

        if (!response.IsSuccessStatusCode)
        {
            JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            throw new InvalidOperationException(errorResponse.GetRawText());
        }

        var serverInfo = await response.Content.ReadFromJsonAsync<JsonObject>();

        return serverInfo!;
    }

    public async Task<JsonObject> RequestConsoleAccessAsync(int serverId)
    {
        var client = await GetClientAsync();
        
        client.DefaultRequestHeaders.Host = "webapi.townshiptale.com";
        var response = await client.PostAsync($"api/servers/{serverId}/console", JsonContent.Create(new { should_launch=true, ignore_offline=true}));
        
        if (!response.IsSuccessStatusCode)
        {
            JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            throw new InvalidOperationException(errorResponse.GetRawText());
        }

        var consoleAccess = await response.Content.ReadFromJsonAsync<JsonObject>();
        return consoleAccess!;
    }
}
