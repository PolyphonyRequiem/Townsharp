namespace Townsharp.Infrastructure.Configuration;

public record UserCredential(string Username, string PasswordHash)
{
    public bool IsConfigured => !(string.IsNullOrEmpty(this.Username) || string.IsNullOrEmpty(this.PasswordHash));
}