namespace Townsharp.Infrastructure.Configuration;

public record BotCredential(string ClientId, string ClientSecret)
{
    public bool IsConfigured => !(String.IsNullOrEmpty(this.ClientId) || String.IsNullOrEmpty(this.ClientSecret));
}
