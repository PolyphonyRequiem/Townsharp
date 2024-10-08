﻿using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Models;
using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Identity;

internal class BotTokenProvider
{
    private record struct TokenResponse(string access_token, string token_type, int expires_in, string scope);
    private const string BaseUri = "https://accounts.townshiptale.com/connect/token";
    private const string DefaultScopes = "ws.group ws.group_members ws.group_servers ws.group_bans ws.group_invites group.info group.join group.leave group.view group.members group.invite server.view server.console";
    private readonly AsyncCache<string> tokenCache;
    private readonly AsyncRetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromSeconds(5));
    private readonly ILogger<BotTokenProvider> logger;

    internal bool IsEnabled => true;

    internal BotTokenProvider(BotCredential botCredential, IHttpClientFactory httpClientFactory, ILogger<BotTokenProvider> logger)
    {
        this.logger = logger;

        var request = new Dictionary<string, string>
        {
            {"grant_type", "client_credentials"},
            {"scope", DefaultScopes},
            {"client_id", botCredential.ClientId},
            {"client_secret", botCredential.ClientSecret}
        };

        FormUrlEncodedContent content = new FormUrlEncodedContent(request);

        this.tokenCache = new AsyncCache<string>(
            TimeSpan.FromMinutes(15),
            async (CancellationToken cancellationToken) =>
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return await retryPolicy.ExecuteAsync<CacheState<string>>(
                    async (CancellationToken cancellationToken) =>
                    {
                        using var httpClient = httpClientFactory.CreateClient();
                        var result = await httpClient.PostAsync(BaseUri, content);

                        if (!result.IsSuccessStatusCode)
                        {
                            this.logger.LogWarning("An error occurred while fetching a bot token: {StatusCode} {ReasonPhrase}", result.StatusCode, result.ReasonPhrase);
                            throw new InvalidOperationException(await result.Content.ReadAsStringAsync());
                        }

                        var response = (await result.Content.ReadFromJsonAsync<TokenResponse>())!;
                        return new CacheState<string>(response.access_token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(response.expires_in));
                    },
                    cts.Token);
            },
            new CacheState<string>("", DateTimeOffset.MinValue));
    }

    internal ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return this.tokenCache.GetAsync(cancellationToken);
    }

    internal async ValueTask<int> GetBotUserIdAsync(CancellationToken cancellationToken = default)
    {
        var token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var claims = JwtDecoder.DecodeJwtClaims(token);

        string userIdString = claims["client_sub"]?.GetValue<string>() ?? throw new InvalidOperationException("Unable to find client_sub claim on JWT, so not bot user id could be determined.");
        return int.Parse(userIdString);
    }

    internal async ValueTask<string> GetBotUserNameAsync(CancellationToken cancellationToken = default)
    {
        var token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var claims = JwtDecoder.DecodeJwtClaims(token);

        return claims["client_username"]?.GetValue<string>() ?? throw new InvalidOperationException("Unable to find client_username claim on JWT, so not bot user id could be determined.");
    }

    internal async ValueTask<UserInfo> GetBotUserInfoAsync(CancellationToken cancellationToken = default)
    {
        return new UserInfo(await GetBotUserIdAsync(cancellationToken), await GetBotUserNameAsync(cancellationToken));
    }
}