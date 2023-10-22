namespace Townsharp.Infrastructure.Configuration;

public record UserCredential(string Username, string PasswordHash)
{
    public static UserCredential FromEnvironmentVariables() => new UserCredential(
            Username: Environment.GetEnvironmentVariable("TOWNSHARP_USERNAME") ?? throw new ArgumentException("Environment Variable TOWNSHARP_USERNAME must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"),
            PasswordHash: Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORDHASH") ?? throw new ArgumentException("TOWNSHARP_PASSWORDHASH must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'"));
}