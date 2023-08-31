namespace Townsharp.Infrastructure.Identity;

internal class DisabledUserTokenProvider : IUserTokenProvider
{
    public bool IsEnabled => false;

    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}