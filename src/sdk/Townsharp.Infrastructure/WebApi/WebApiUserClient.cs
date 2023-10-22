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

    public WebApiUserClient(UserCredential credential)
        : this(new UserTokenProvider(credential, InternalHttpClientFactory.Default), InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiUserClient>())
    {
    }

    public WebApiUserClient()
        : this(InternalUserTokenProvider.Default, InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiUserClient>())
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

    public async Task<WebApiResult<GroupInfoDetailed>> GetGroupAsync(int groupId)
    {
        var client = await GetClientAsync();

        var response = await client.GetAsync($"api/groups/{groupId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<GroupInfoDetailed>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<GroupInfoDetailed>.Success(rawContent);
    }

    public async IAsyncEnumerable<JoinedGroupInfo> GetJoinedGroupsAsync()
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
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedGroup in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return JsonSerializer.Deserialize<JoinedGroupInfo>(joinedGroup, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response");
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    public async IAsyncEnumerable<InvitedGroupInfo> GetPendingGroupInvitationsAsync()
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
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedGroup in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return JsonSerializer.Deserialize<InvitedGroupInfo>(joinedGroup, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response");
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    // throws 400 if the invite has already been accepted
    public async Task<WebApiResult<GroupMemberInfo>> AcceptGroupInviteAsync(int groupId)
    {
        var client = await GetClientAsync();
        var response = await client.PostAsync($"api/groups/invites/{groupId}", new StringContent(groupId.ToString()));

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<GroupMemberInfo>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<GroupMemberInfo>.Success(rawContent);
    }

    public async Task<WebApiResult<GroupMemberInfo>> GetGroupMemberAsync(int groupId, int userId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/groups/{groupId}/members/{userId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<GroupMemberInfo>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<GroupMemberInfo>.Success(rawContent);
    }

    public async IAsyncEnumerable<ServerInfo> GetJoinedServersAsync()
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
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
            }

            lastPaginationToken = response.Headers.Contains("paginationToken") ?
                response.Headers.GetValues("paginationToken").First() :
                string.Empty;

            foreach (var joinedServer in await response.Content.ReadFromJsonAsync<JsonArray>() ?? new JsonArray())
            {
                yield return JsonSerializer.Deserialize<ServerInfo>(joinedServer, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response");
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    public async Task<WebApiResult<ServerInfo>> GetServerAsync(int serverId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/servers/{serverId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<ServerInfo>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<ServerInfo>.Success(rawContent);
    }

    public async Task<WebApiResult<ConsoleAccess>> RequestConsoleAccessAsync(int serverId)
    {
        var client = await GetClientAsync();

        client.DefaultRequestHeaders.Host = "webapi.townshiptale.com";
        var response = await client.PostAsync($"api/servers/{serverId}/console", JsonContent.Create(new { should_launch = true, ignore_offline = true }));

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<ConsoleAccess>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<ConsoleAccess>.Success(rawContent);
    }
}
