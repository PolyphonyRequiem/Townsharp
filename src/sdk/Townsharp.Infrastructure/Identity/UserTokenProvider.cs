using System.Net.Http.Json;

using Polly;
using Polly.Retry;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Identity;

public class UserTokenProvider : IUserTokenProvider
{
    private record struct TokenResponse(string access_token, string token_type, int expires_in, string scope);
    private const string BaseUri = "https://webapi.townshiptale.com/api/sessions";
    private readonly AsyncCache<string> tokenCache;
    private readonly AsyncRetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromSeconds(5));

    public bool IsEnabled => true;

    public UserTokenProvider(UserCredential userCredential, HttpClient httpClient)
    {
        var request = new Dictionary<string, string>
        {
            {"username", userCredential.Username},
            {"password_hash", userCredential.PasswordHash}
        };

        if (!userCredential.IsConfigured)
        {
            throw new InvalidOperationException("Unable to use an unconfigured user credential. Make sure both Username and PasswordHash are set.");
        }

        JsonContent content = JsonContent.Create(request);

        httpClient.DefaultRequestHeaders.Add("x-api-key", "2l6aQGoNes8EHb94qMhqQ5m2iaiOM9666oDTPORf");

        // This is not (yet) fault tolerant, but it needs to be, so we could use Polly for this, but if we are going to keep the code free of dependencies, we need to write our own retry logic.
        this.tokenCache = new AsyncCache<string>(
            TimeSpan.FromMinutes(15),
            async (CancellationToken cancellationToken) =>
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return await retryPolicy.ExecuteAsync(
                    async (CancellationToken cancellationToken) => 
                    {
                        var result = await httpClient.PostAsync(BaseUri, content);

                        if (!result.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(await result.Content.ReadAsStringAsync());
                        }

                        var response = (await result.Content.ReadFromJsonAsync<TokenResponse>())!;
                        return new CacheState<string>(response.access_token, DateTimeOffset.UtcNow + TimeSpan.FromHours(.9));
                    },
                    cts.Token);
            },
            new CacheState<string>("", DateTimeOffset.MinValue));
    }

    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return this.tokenCache.GetAsync(cancellationToken);
    }
}