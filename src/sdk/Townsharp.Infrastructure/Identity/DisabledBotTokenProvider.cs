namespace Townsharp.Infrastructure.Identity;

public class DisabledBotTokenProvider : IBotTokenProvider
{
    public bool IsEnabled => false;

    public ValueTask<int> GetBotUserIdAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string> GetBotUserNameAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}