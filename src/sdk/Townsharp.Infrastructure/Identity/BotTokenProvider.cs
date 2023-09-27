using System.Net.Http.Json;

using Polly;
using Polly.Retry;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Identity;

public class BotTokenProvider : IBotTokenProvider
{
    private record struct TokenResponse(string access_token, string token_type, int expires_in, string scope);
    private const string BaseUri = "https://accounts.townshiptale.com/connect/token";
    private const string DefaultScopes = "ws.group ws.group_members ws.group_servers ws.group_bans ws.group_invites group.info group.join group.leave group.view group.members group.invite server.view server.console";
    private readonly AsyncCache<string> tokenCache;
    private readonly AsyncRetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromSeconds(5));

    public bool IsEnabled => true;

    public BotTokenProvider(BotCredential botCredential, HttpClient httpClient)
    {
        var request = new Dictionary<string, string>
        {
            {"grant_type", "client_credentials"},
            {"scope", DefaultScopes},
            {"client_id", botCredential.ClientId},
            {"client_secret", botCredential.ClientSecret}
        };

        if (!botCredential.IsConfigured)
        {
            throw new InvalidOperationException("Unable to use an unconfigured bot credential. Make sure both ClientId and ClientSecret are set.");
        }

        FormUrlEncodedContent content = new FormUrlEncodedContent(request);

        this.tokenCache = new AsyncCache<string>(
            TimeSpan.FromMinutes(15),
            async (CancellationToken cancellationToken) =>
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return await retryPolicy.ExecuteAsync<CacheState<string>>(
                    async (CancellationToken cancellationToken) => {
                        var result = await httpClient.PostAsync(BaseUri, content);

                        if (!result.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(await result.Content.ReadAsStringAsync());
                        }

                        var response = (await result.Content.ReadFromJsonAsync<TokenResponse>())!;
                        return new CacheState<string>(response.access_token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(response.expires_in));
                    },
                    cts.Token);
            },
            new CacheState<string>("", DateTimeOffset.MinValue));
    }

    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return this.tokenCache.GetAsync(cancellationToken);
    }

    public async ValueTask<int> GetBotUserIdAsync(CancellationToken cancellationToken = default)
    {
        var token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var claims = JwtDecoder.DecodeJwtClaims(token);

        string userIdString = claims["client_sub"]?.GetValue<string>() ?? throw new InvalidOperationException("Unable to find client_sub claim on JWT, so not bot user id could be determined.");
        return int.Parse(userIdString);
    }

    public async ValueTask<string> GetBotUserNameAsync(CancellationToken cancellationToken = default)
    {
        var token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var claims = JwtDecoder.DecodeJwtClaims(token);

        return claims["client_username"]?.GetValue<string>() ?? throw new InvalidOperationException("Unable to find client_username claim on JWT, so not bot user id could be determined.");
    }
}