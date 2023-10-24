using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Polly;
using Polly.Retry;

using Townsharp.Infrastructure.Composition;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Utilities;

namespace Townsharp.Infrastructure.Identity;

internal class UserTokenProvider
{
    private record struct TokenResponse(string access_token, string token_type, int expires_in, string scope);
    private const string BaseUri = "https://webapi.townshiptale.com/api/sessions";
    private readonly AsyncCache<string> tokenCache;
    private readonly AsyncRetryPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromSeconds(5));

    internal bool IsEnabled => true;

    internal UserTokenProvider(UserCredential userCredential)
    : this(userCredential, InternalHttpClientFactory.Default)
    {

    }


    internal UserTokenProvider(UserCredential userCredential, IHttpClientFactory httpClientFactory)
    {
        var request = new Dictionary<string, string>
        {
            {"username", userCredential.Username}
        };

        // Prefer PasswordHash over Password
        if (userCredential.PasswordHash == String.Empty)
        {
            request.Add("password_hash", userCredential.PasswordHash);
        }
        else
        {
            using (SHA512 sha512Hash = SHA512.Create())
            {
                byte[] bytes = sha512Hash.ComputeHash(Encoding.UTF8.GetBytes(userCredential.Password));

                StringBuilder passwordHashBuilder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    passwordHashBuilder.Append(bytes[i].ToString("x2"));
                }

                request.Add("password_hash", passwordHashBuilder.ToString()); 
            }
        }

        JsonContent content = JsonContent.Create(request);



        // This is not (yet) fault tolerant, but it needs to be, so we could use Polly for this, but if we are going to keep the code free of dependencies, we need to write our own retry logic.
        this.tokenCache = new AsyncCache<string>(
            TimeSpan.FromMinutes(15),
            async (CancellationToken cancellationToken) =>
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return await retryPolicy.ExecuteAsync(
                    async (CancellationToken cancellationToken) =>
                    {
                        using var httpClient = httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.Add("x-api-key", "2l6aQGoNes8EHb94qMhqQ5m2iaiOM9666oDTPORf");
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

    internal ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return this.tokenCache.GetAsync(cancellationToken);
    }
}