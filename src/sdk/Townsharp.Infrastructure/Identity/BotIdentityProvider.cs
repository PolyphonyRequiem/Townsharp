using Townsharp.Infrastructure.Models;

namespace Townsharp.Infrastructure.Identity;

/// <summary>
/// Provides access to the Bot's identity.
/// </summary>

public class BotIdentityProvider
{
   private readonly BotTokenProvider botTokenProvider;

   internal BotIdentityProvider(BotTokenProvider botTokenProvider)
   {
      this.botTokenProvider = botTokenProvider;
   }

   /// <summary>
   /// Gets the currently logged in bot's user information.
   /// </summary>
   /// <param name="cancellationToken"></param>
   /// <returns>The <see cref="UserInfo"/> representing the logged in bot user.</returns>
   public async ValueTask<UserInfo> GetBotUserInfoAsync(CancellationToken cancellationToken = default)
   {
      return await this.botTokenProvider.GetBotUserInfoAsync(cancellationToken).ConfigureAwait(false);
   }
}
