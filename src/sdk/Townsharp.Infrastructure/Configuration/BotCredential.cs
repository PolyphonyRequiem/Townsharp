namespace Townsharp.Infrastructure.Configuration;

/// <summary>
/// The credentials for a bot as provided by Alta.
/// </summary>
/// <param name="ClientId">The Client Id of the bot. Should begin with "client_"</param>
/// <param name="ClientSecret">The Client Secret or "Token" of the bot.</param>
public record BotCredential(string ClientId, string ClientSecret)
{
    /// <summary>
    /// Generates the <see cref="BotCredential"/> from the environment variables TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET."/>
    /// </summary>
    /// <returns>The <see cref="BotCredential"/> generated from the environment variables TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static BotCredential FromEnvironmentVariables() => new BotCredential(
            ClientId: Environment.GetEnvironmentVariable("TOWNSHARP_CLIENTID") ?? throw new ArgumentException("Environment Variable TOWNSHARP_CLIENTID must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"),
            ClientSecret: Environment.GetEnvironmentVariable("TOWNSHARP_CLIENTSECRET") ?? throw new ArgumentException("TOWNSHARP_CLIENTSECRET must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"));
}
