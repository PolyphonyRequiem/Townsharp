using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.CommonModels;
using Townsharp.Infrastructure.Composition;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiBotClient
{
    internal const int PaginationLimit = 500;
    internal const string BaseAddress = "https://webapi.townshiptale.com/";
    private readonly BotTokenProvider botTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebApiBotClient> logger;

    public WebApiBotClient(BotCredential credential)
        : this(new BotTokenProvider(credential, InternalHttpClientFactory.Default), InternalHttpClientFactory.Default, InternalLoggerFactory.Default.CreateLogger<WebApiBotClient>())
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

    public async Task<WebApiResult<JsonObject>> GetGroupAsync(int groupId)
    {
        var client = await GetClientAsync();
        
        var response = await client.GetAsync($"api/groups/{groupId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<JsonObject>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<JsonObject>.Success(rawContent);
    }

    public async IAsyncEnumerable<JsonNode> GetJoinedGroupsAsync()
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
                yield return joinedGroup?.AsObject() ?? new JsonObject();
            }
        }
        while (response.Headers.Contains("paginationToken"));
    }

    public async IAsyncEnumerable<JsonNode> GetPendingGroupInvitationsAsync()
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
    public async Task<WebApiResult<JsonObject>> AcceptGroupInviteAsync(int groupId)
    {
        var client = await GetClientAsync();
        var response = await client.PostAsync($"api/groups/invites/{groupId}", new StringContent(groupId.ToString()));

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<JsonObject>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<JsonObject>.Success(rawContent);
    }

    public async Task<WebApiResult<JsonObject>> GetGroupMemberAsync(int groupId, int userId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/groups/{groupId}/members/{userId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<JsonObject>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<JsonObject>.Success(rawContent);
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
                JsonNode errorResponse = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
                throw new InvalidOperationException(errorResponse.ToJsonString());
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

    public async Task<WebApiResult<JsonObject>> GetServerAsync(int serverId)
    {
        var client = await GetClientAsync();
        var response = await client.GetAsync($"api/servers/{serverId}");

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<JsonObject>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<JsonObject>.Success(rawContent);
    }

    public async Task<WebApiResult<ConsoleAccess>> RequestConsoleAccessAsync(int serverId)
    {
        var client = await GetClientAsync();
        
        client.DefaultRequestHeaders.Host = "webapi.townshiptale.com";
        var response = await client.PostAsync($"api/servers/{serverId}/console", JsonContent.Create(new { should_launch=true, ignore_offline=true}));

        var rawContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WebApiResult<ConsoleAccess>.Failure(rawContent, $"The response status did not indicated success. {response.StatusCode}");
        }

        return WebApiResult<ConsoleAccess>.Success(rawContent);
    }

    public async Task<UserInfo> GetBotUserInfoAsync()
    {
        return await this.botTokenProvider.GetBotUserInfoAsync();
    }
}
