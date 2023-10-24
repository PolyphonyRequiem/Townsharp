namespace Townsharp.Infrastructure.Configuration;

public record UserCredential(string Username, string PasswordHash, string Password)
{
    public static UserCredential FromEnvironmentVariables()
    {
        var username = Environment.GetEnvironmentVariable("TOWNSHARP_USERNAME") ?? throw new ArgumentException("Environment Variable TOWNSHARP_USERNAME must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'");
        var password = Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORD");
        var passwordHash = Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORDHASH");

        if (password is null && passwordHash is null)
        {
            throw new ArgumentException("Either TOWNSHARP_PASSWORD or TOWNSHARP_PASSWORDHASH must be set. Please review 'Getting Started' documentation section for 'Identity, Authentication and Authorization'");
        }

        return new UserCredential(username, passwordHash ?? String.Empty, password ?? String.Empty);
    }
}