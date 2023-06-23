using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Identity;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiClient
{
    public const int Limit = 500;
    public const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly BotTokenProvider botTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiClient> logger;
    
    // API could actually be able to yield it's own tokens here actually, it's clearly in the ApiClient's domain
    // private readonly ClientProvider getUserHttpClient;

    public WebApiClient(BotTokenProvider botTokenProvider, IHttpClientFactory httpClientFactory, ILogger<WebApiClient> logger)
    {
        this.botTokenProvider = botTokenProvider;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    private async Task<HttpClient> GetClientAsync()
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(WebApiClient.BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await this.botTokenProvider.GetTokenAsync());
        return httpClient;
    }

    public async Task<JsonObject> GetGroupAsync(long groupId)
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
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/joined?limit={Limit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/joined?limit={Limit}");

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
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/invites?limit={Limit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/groups/invites?limit={Limit}");

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
    public async Task<bool> AcceptGroupInviteAsync(long groupId)
    {
        var client = await GetClientAsync();
        var response = await client.PostAsync($"api/groups/invites/{groupId}", new StringContent(groupId.ToString()));
        return response.IsSuccessStatusCode;
    }

    public async Task<JsonObject> GetGroupMemberAsync(long groupId, long userId)
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
                new HttpRequestMessage(HttpMethod.Get, $"api/servers/joined?limit={Limit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"api/servers/joined?limit={Limit}");

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

    public async Task<JsonObject> GetServerAsync(long serverId)
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

    public async Task<JsonObject> RequestConsoleAccessAsync(long serverId)
    {
        var client = await GetClientAsync();
        var response = await client.PostAsync($"api/servers/{serverId}", new StringContent("{\"should_launch\":true, \"ignore_offline\":true}"));
        
        if (!response.IsSuccessStatusCode)
        {
            JsonElement errorResponse = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            throw new InvalidOperationException(errorResponse.GetRawText());
        }

        var consoleAccess = await response.Content.ReadFromJsonAsync<JsonObject>();
        return consoleAccess!;
    }
}
