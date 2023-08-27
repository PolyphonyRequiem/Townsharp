namespace Townsharp.Infrastructure.Identity;

public interface IUserTokenProvider
{
    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default);

    public bool IsEnabled { get; }
}
