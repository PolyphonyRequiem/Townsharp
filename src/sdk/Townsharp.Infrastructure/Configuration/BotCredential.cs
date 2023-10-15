namespace Townsharp.Infrastructure.Configuration;

public record BotCredential(string ClientId, string ClientSecret)
{
    public static BotCredential FromEnvironmentVariables() => new BotCredential(
            ClientId: Environment.GetEnvironmentVariable("TOWNSHARP_CLIENTID") ?? throw new ArgumentException("Environment Variable TOWNSHARP_CLIENTID must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"),
            ClientSecret: Environment.GetEnvironmentVariable("TOWNSHARP_CLIENTSECRET") ?? throw new ArgumentException("TOWNSHARP_CLIENTSECRET must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"));
}
