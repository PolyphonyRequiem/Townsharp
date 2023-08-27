namespace Townsharp.Infrastructure.Identity.Models;

public record UserCredential(string Username, string PasswordHash)
{
    public bool IsConfigured => !(String.IsNullOrEmpty(this.Username) || String.IsNullOrEmpty(this.PasswordHash));
}