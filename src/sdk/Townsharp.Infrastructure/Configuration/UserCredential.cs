namespace Townsharp.Infrastructure.Configuration;

/// <summary>
/// The credentials for a user used to authenticate with Alta.
/// </summary>
/// <param name="Username">The username of the user.</param>
/// <param name="PasswordHash">The password hash of the user.  This is the SHA512 hash of the password and is the preferred method of calling the service.  This value will be used if present over the <paramref name="Password"/> value</param>
/// <param name="Password">The password of the user.  This value will be used if <paramref name="PasswordHash"/> is not present. If neither are present, login will fail.</param>
public record UserCredential(string Username, string PasswordHash, string Password)
{
    /// <summary>
    /// Generates the <see cref="UserCredential"/> from the environment variables TOWNSHARP_USERNAME, TOWNSHARP_PASSWORDHASH and TOWNSHARP_PASSWORD."/>
    /// </summary>
    /// <returns>The <see cref="UserCredential"/> generated from the environment variables TOWNSHARP_USERNAME, TOWNSHARP_PASSWORDHASH and TOWNSHARP_PASSWORD.</returns>
    /// <exception cref="ArgumentException"></exception>
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