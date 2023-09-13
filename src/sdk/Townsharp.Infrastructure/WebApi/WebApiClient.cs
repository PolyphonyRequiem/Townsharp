using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiClient
{
    public const int Limit = 500;
    public const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly IBotTokenProvider botTokenProvider;
    private readonly IUserTokenProvider userTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiClient> logger;
    private readonly bool preferUserToken;

    // API could actually be able to yield it's own tokens here actually, it's clearly in the ApiClient's domain
    // private readonly ClientProvider getUserHttpClient;

    // I don't like this model.  I'd rather see the identity attached to the client, which may mean more than one client type.  It's fine for now, but worth thinking about.
    public WebApiClient(IBotTokenProvider botTokenProvider, IUserTokenProvider userTokenProvider, IHttpClientFactory httpClientFactory, ILogger<WebApiClient> logger, bool preferUserToken = false)
    {
        this.botTokenProvider = botTokenProvider;
        this.userTokenProvider = userTokenProvider;
        if (!this.botTokenProvider.IsEnabled && !this.userTokenProvider.IsEnabled)
        {
            throw new InvalidOperationException("Unable to use WebApiClient without at least an enabled UserTokenProvider or BotTokenProvider.");
        }
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.preferUserToken = preferUserToken;
    }

    enum Provider
    {
        BotRequired,
        UserRequired,
        Either
    }

    private async Task<string> GetTokenAsync(Provider provider, [CallerMemberName] string? callerMemberName = default)
    {
        Func<CancellationToken, ValueTask<string>> preferredProviderMethod;

        if (this.preferUserToken)
        {
            if (this.userTokenProvider.IsEnabled)
            {
                preferredProviderMethod = this.userTokenProvider.GetTokenAsync;
            }
            else if (this.botTokenProvider.IsEnabled)
            {
                preferredProviderMethod = this.botTokenProvider.GetTokenAsync;
            }
            else
            {
                throw new InvalidOperationException($"Unable to handle the request {callerMemberName} using the preferred token provider, as neither bot nor user token provider are configured.");
            }
        }
        else
        {
            if (this.botTokenProvider.IsEnabled)
            {
                preferredProviderMethod = this.botTokenProvider.GetTokenAsync;
            }
            else if (this.userTokenProvider.IsEnabled)
            {
                preferredProviderMethod = this.userTokenProvider.GetTokenAsync;
            }
            else
            {
                throw new InvalidOperationException($"Unable to handle the request {callerMemberName} using the preferred token provider, as neither bot nor user token provider are configured.");
            }
        }

        return provider switch
        {
            Provider.BotRequired => this.botTokenProvider.IsEnabled ? await this.botTokenProvider.GetTokenAsync() : throw new InvalidOperationException($"Caller {callerMemberName ?? ""} requires a configured BotTokenProvider.  Please verify that you have configured your bot token credentials correctly."),
            Provider.UserRequired => this.userTokenProvider.IsEnabled ? await this.userTokenProvider.GetTokenAsync() : throw new InvalidOperationException($"Caller {callerMemberName ?? ""} requires a configured UserTokenProvider.  Please verify that you have configured your user token credentials correctly."),
            Provider.Either => await preferredProviderMethod(CancellationToken.None),
            _ => throw new NotImplementedException()
        };
    }

    private async Task<HttpClient> GetClientAsync(Provider provider = Provider.Either)
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(WebApiClient.BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await this.GetTokenAsync(provider));
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
