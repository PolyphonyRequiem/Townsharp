using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Models;
using Townsharp.Infrastructure.Composition;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiBotClient
{
    private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = WebApiSerializerContext.Default
    };

    internal const int PaginationLimit = 500;
    internal const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly BotTokenProvider botTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiBotClient> logger;

    public WebApiBotClient(BotCredential credential)
        : this(new BotTokenProvider(credential), InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiBotClient>())
    {
    }

    public WebApiBotClient()
        : this(InternalBotTokenProvider.Default, InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiBotClient>())
    {
    }

    internal WebApiBotClient(BotTokenProvider botTokenProvider, IHttpClientFactory httpClientFactory, ILogger<WebApiBotClient> logger)
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
        httpClient.BaseAddress = new Uri(WebApiBotClient.BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await this.GetTokenAsync());
        return httpClient;
    }

    public async Task<WebApiResult<GroupInfoDetailed>> GetGroupAsync(int groupId)
    {
        return await SendRequestAsync<GroupInfoDetailed>($"api/groups/{groupId}", HttpMethod.Get);
    }

    public IAsyncEnumerable<JoinedGroupInfo> GetJoinedGroupsAsyncStream()
    {
        return GetResultsAsyncStream<JoinedGroupInfo>("api/groups/joined");
    }

    public async Task<IEnumerable<JoinedGroupInfo>> GetJoinedGroupsAsync()
    {
        return await GetPaginatedResultsAsync<JoinedGroupInfo>("api/groups/joined");
    }

    public async Task<IEnumerable<InvitedGroupInfo>> GetPendingGroupInvitesAsync()
    {
        return await GetPaginatedResultsAsync<InvitedGroupInfo>("api/groups/invites");
    }

    public IAsyncEnumerable<InvitedGroupInfo> GetPendingGroupInvitationsAsyncStream()
    {
        return GetResultsAsyncStream<InvitedGroupInfo>("api/groups/invites");
    }

    // throws 400 if the invite has already been accepted
    public async Task<WebApiResult<GroupMemberInfo>> AcceptGroupInviteAsync(int groupId)
    {
        return await SendRequestAsync<GroupMemberInfo>($"api/groups/invites/{groupId}", HttpMethod.Post);
    }

    public async Task<WebApiResult<GroupMemberInfo>> GetGroupMemberAsync(int groupId, int userId)
    {
        return await SendRequestAsync<GroupMemberInfo>($"api/groups/{groupId}/members/{userId}", HttpMethod.Get);
    }

    public async Task<IEnumerable<ServerInfo>> GetJoinedServersAsync()
    {
        return await GetPaginatedResultsAsync<ServerInfo>("api/servers/joined");
    }

    public IAsyncEnumerable<ServerInfo> GetJoinedServersAsyncStream()
    { 
        return GetResultsAsyncStream<ServerInfo>("api/servers/joined");
    }

    public async Task<WebApiResult<ServerInfo>> GetServerAsync(int serverId)
    {
        return await SendRequestAsync<ServerInfo>($"api/servers/{serverId}", HttpMethod.Get);
    }

    public async Task<WebApiResult<ConsoleAccess>> RequestConsoleAccessAsync(int serverId)
    {
        return await SendRequestAsync<ConsoleAccess>(
            $"api/servers/{serverId}/console",
            HttpMethod.Post,
            JsonContent.Create(new { should_launch = true, ignore_offline = true }));
    }

    public async Task<UserInfo> GetBotUserInfoAsync()
    {
        return await this.botTokenProvider.GetBotUserInfoAsync();
    }

    public async Task<WebApiResult<TResult>> SendRequestAsync<TResult>(string route, HttpMethod method, JsonContent? content = default)
    {
        var client = await GetClientAsync();

        client.DefaultRequestHeaders.Host = "webapi.townshiptale.com";

        var message = content == default ?
            new HttpRequestMessage(method, route) :
            new HttpRequestMessage(method, route) { Content = content };

        var response = await client.SendAsync(message);

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<TResult>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<TResult>.Success(rawContent);
    }

    private async Task<IEnumerable<TResult>> GetPaginatedResultsAsync<TResult>(string route)
    {
        var client = await GetClientAsync();
        HttpResponseMessage response;
        string lastPaginationToken = string.Empty;

        List<TResult> allResults = new List<TResult>();

        do
        {
            var message = lastPaginationToken != string.Empty ?
                new HttpRequestMessage(HttpMethod.Get, $"{route}?limit={PaginationLimit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"{route}?limit={PaginationLimit}");

            response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedGroup in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                allResults.Add(JsonSerializer.Deserialize<TResult>(joinedGroup, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response"));
            }
        }
        while (response.Headers.Contains("paginationToken"));

        return allResults;
    }

    private async IAsyncEnumerable<TResult> GetResultsAsyncStream<TResult>(string route)
    {
        var client = await GetClientAsync();
        HttpResponseMessage response;
        string lastPaginationToken = string.Empty;

        do
        {
            var message = lastPaginationToken != string.Empty ?
                new HttpRequestMessage(HttpMethod.Get, $"{route}?limit={PaginationLimit}&paginationToken={lastPaginationToken}") :
                new HttpRequestMessage(HttpMethod.Get, $"{route}?limit={PaginationLimit}");

            response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedGroup in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return JsonSerializer.Deserialize<TResult>(joinedGroup, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response");
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }
}
