namespace Townsharp.Infrastructure.Identity;

public interface IBotTokenProvider
{
    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default);

    public bool IsEnabled { get; }
}
