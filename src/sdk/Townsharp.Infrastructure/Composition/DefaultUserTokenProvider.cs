using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.Composition;

internal static class InternalUserTokenProvider
{
    private static UserTokenProvider defaultInstance = new UserTokenProvider(UserCredential.FromEnvironmentVariables());

    internal static UserTokenProvider Default => defaultInstance;
}
