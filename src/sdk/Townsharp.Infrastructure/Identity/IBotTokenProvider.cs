namespace Townsharp.Infrastructure.Identity;

public interface IBotTokenProvider
{
    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default);

    public ValueTask<int> GetBotUserIdAsync(CancellationToken cancellationToken = default);

    public ValueTask<string> GetBotUserNameAsync(CancellationToken cancellationToken = default);

    public bool IsEnabled { get; }
}
