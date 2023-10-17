using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.Composition;

internal static class InternalBotTokenProvider
{
    private static BotTokenProvider defaultInstance = new BotTokenProvider(BotCredential.FromEnvironmentVariables());

    internal static BotTokenProvider Default => defaultInstance;
}
