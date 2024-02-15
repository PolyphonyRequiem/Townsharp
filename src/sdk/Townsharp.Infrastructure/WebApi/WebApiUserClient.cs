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

public class WebApiUserClient
{
    private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = WebApiSerializerContext.Default
    };

    internal const int PaginationLimit = 500;
    internal const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly UserTokenProvider userTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiUserClient> logger;

    /// <summary>
    /// Creates a new instance of the WebApiUserClient using the default <see cref="ILoggerFactory"/> and <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <param name="credential">The <see cref="UserCredential"/> to use for authentication.</param>
    public WebApiUserClient(UserCredential credential)
        : this(new UserTokenProvider(credential), InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiUserClient>())
    {
    }

    /// <summary>
    /// Creates a new instance of the WebApiUserClient.
    /// </summary>
    /// <param name="credential">The <see cref="UserCredential"/> to use for authentication.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use for creating <see cref="HttpClient"/> instances.</param>
    /// <param name="logger">The <see cref="ILogger{T}"/> to use for logging.</param>
    public WebApiUserClient(UserCredential credential, IHttpClientFactory httpClientFactory, ILogger<WebApiUserClient> logger)
        : this(new UserTokenProvider(credential), httpClientFactory, logger)
    {
    }

    internal WebApiUserClient(UserTokenProvider userTokenProvider, IHttpClientFactory httpClientFactory, ILogger<WebApiUserClient> logger)
    {
        this.userTokenProvider = userTokenProvider;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    private async Task<string> GetTokenAsync([CallerMemberName] string? callerMemberName = default)
        => this.userTokenProvider.IsEnabled ?
            await this.userTokenProvider.GetTokenAsync() :
            throw new InvalidOperationException($"Caller {callerMemberName ?? ""} requires a configured UserTokenProvider.  Please verify that you have configured your UserCredentials correctly.");

    private async Task<HttpClient> GetClientAsync()
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(WebApiBotClient.BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await this.GetTokenAsync());
        return httpClient;
    }

    /// <summary>
    /// Gets information about a group by its ID.
    /// </summary>
    /// <param name="groupId">The ID of the group to get information about.</param>
    /// <returns>A <see cref="WebApiResult{T}"/> containing the <see cref="GroupInfoDetailed"/> if successful, or an error message if not.</returns>
    public async Task<WebApiResult<GroupInfoDetailed>> GetGroupAsync(int groupId)
    {
        return await SendRequestAsync<GroupInfoDetailed>($"api/groups/{groupId}", HttpMethod.Get);
    }

    /// <summary>
    /// Gets a list of all groups the user is a member of.
    /// </summary>
    public async Task<IEnumerable<JoinedGroupInfo>> GetJoinedGroupsAsync()
    {
        return await GetPaginatedResultsAsync<JoinedGroupInfo>("api/groups/joined");
    }

    /// <summary>
    /// Gets a list of all groups the user is a member of as an asynchronous sequence via <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <returns>An asynchronous sequence of <see cref="JoinedGroupInfo"/> instances.</returns>
    public IAsyncEnumerable<JoinedGroupInfo> GetJoinedGroupsAsyncStream()
    {
        return GetResultsAsyncStream<JoinedGroupInfo>("api/groups/joined");
    }

    /// <summary>
    /// Gets a list of all groups the user has been invited to.
    /// </summary>
    /// <returns>A list of <see cref="InvitedGroupInfo"/> instances.</returns>
    public async Task<IEnumerable<InvitedGroupInfo>> GetPendingGroupInvitesAsync()
    {
        return await GetPaginatedResultsAsync<InvitedGroupInfo>("api/groups/invites");
    }

    /// <summary>
    /// Gets a list of all groups the user has been invited to as an asynchronous sequence via <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <returns>An asynchronous sequence of <see cref="InvitedGroupInfo"/> instances.</returns>
    public IAsyncEnumerable<InvitedGroupInfo> GetPendingGroupInvitationsAsyncStream()
    {
        return GetResultsAsyncStream<InvitedGroupInfo>("api/groups/invites");
    }

    // throws 400 if the invite has already been accepted
    /// <summary>
    /// Accepts a group invitation.
    /// </summary>
    /// <param name="groupId">The ID of the group to accept the invitation for.</param>
    /// <returns>A <see cref="WebApiResult{T}"/> containing the <see cref="GroupMemberInfo"/> if successful, or an error message if not.</returns>
    public async Task<WebApiResult<GroupMemberInfo>> AcceptGroupInviteAsync(int groupId)
    {
        return await SendRequestAsync<GroupMemberInfo>($"api/groups/invites/{groupId}", HttpMethod.Post);
    }

    /// <summary>
    /// Gets information about a group member.
    /// </summary>
    /// <param name="groupId">The ID of the group the member belongs to.</param>
    /// <param name="userId">The ID of the user to get information about.</param>
    /// <returns>A <see cref="WebApiResult{T}"/> containing the <see cref="GroupMemberInfo"/> if successful, or an error message if not.</returns>
    public async Task<WebApiResult<GroupMemberInfo>> GetGroupMemberAsync(int groupId, int userId)
    {
        return await SendRequestAsync<GroupMemberInfo>($"api/groups/{groupId}/members/{userId}", HttpMethod.Get);
    }

    /// <summary>
    /// Gets a list of all servers the user is a member of.
    /// </summary>
    /// <returns>A list of <see cref="ServerInfo"/> instances.</returns>
    public async Task<IEnumerable<ServerInfo>> GetJoinedServersAsync()
    {
        return await GetPaginatedResultsAsync<ServerInfo>("api/servers/joined");
    }

    /// <summary>
    /// Gets a list of all servers the user is a member of as an asynchronous sequence via <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <returns>An asynchronous sequence of <see cref="ServerInfo"/> instances.</returns>
    public IAsyncEnumerable<ServerInfo> GetJoinedServersAsyncStream()
    {
        return GetResultsAsyncStream<ServerInfo>("api/servers/joined");
    }

    /// <summary>
    /// Gets information about a server by its ID.
    /// </summary>
    /// <param name="serverId">The ID of the server to get information about.</param>
    /// <returns>A <see cref="WebApiResult{T}"/> containing the <see cref="ServerInfo"/> if successful, or an error message if not.</returns>
    public async Task<WebApiResult<ServerInfo>> GetServerAsync(int serverId)
    {
        return await SendRequestAsync<ServerInfo>($"api/servers/{serverId}", HttpMethod.Get);
    }

    /// <summary>
    /// Requests console access for a server.
    /// </summary>
    /// <param name="serverId">The ID of the server to request console access for.</param>
    /// <returns>A <see cref="WebApiResult{T}"/> containing the <see cref="ConsoleAccess"/> if successful, or an error message if not.</returns>
    public async Task<WebApiResult<ConsoleAccess>> RequestConsoleAccessAsync(int serverId)
    {
        return await SendRequestAsync<ConsoleAccess>(
            $"api/servers/{serverId}/console",
            HttpMethod.Post,
            JsonContent.Create(new { should_launch = true, ignore_offline = true }));
    }

    private async Task<WebApiResult<TResult>> SendRequestAsync<TResult>(string route, HttpMethod method, JsonContent? content = default)
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
