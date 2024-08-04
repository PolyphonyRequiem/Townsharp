using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Townsharp.Infrastructure.Configuration;

namespace Townsharp.Infrastructure;

public static class Builders
{
   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential)
   {
      return new BotClientBuilder(botCredential, new NullLoggerFactory());
   }

   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential, ILoggerFactory loggerFactory)
   {
      return new BotClientBuilder(botCredential, loggerFactory);
   }

   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential)
   {
      return new UserClientBuilder(userCredential, new NullLoggerFactory());
   }

   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      return new UserClientBuilder(userCredential, loggerFactory);
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential)
   {
      return new HybridClientBuilder(botCredential, userCredential, new NullLoggerFactory());
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      return new HybridClientBuilder(botCredential, userCredential, loggerFactory);
   }
}
