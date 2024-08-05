using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Townsharp.Infrastructure.Configuration;

namespace Townsharp.Infrastructure;

public static class Builders
{
   internal sealed class DefaultHttpClientFactory : IHttpClientFactory, IDisposable
   {
      private readonly Lazy<HttpMessageHandler> _handlerLazy = new(() => new HttpClientHandler());

      public HttpClient CreateClient(string name) => new(_handlerLazy.Value, disposeHandler: false);

      public void Dispose()
      {
         if (_handlerLazy.IsValueCreated)
         {
            _handlerLazy.Value.Dispose();
         }
      }
   }

   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential)
   {
      return new BotClientBuilder(botCredential, new NullLoggerFactory(), new DefaultHttpClientFactory());
   }

   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential, IHttpClientFactory httpClientFactory)
   {
      return new BotClientBuilder(botCredential, new NullLoggerFactory(), httpClientFactory);
   }

   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential, ILoggerFactory loggerFactory)
   {
      return new BotClientBuilder(botCredential, loggerFactory, new DefaultHttpClientFactory());
   }

   public static BotClientBuilder CreateBotClientBuilder(BotCredential botCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      return new BotClientBuilder(botCredential, loggerFactory, httpClientFactory);
   }

   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential)
   {
      return new UserClientBuilder(userCredential, new NullLoggerFactory(), new DefaultHttpClientFactory());
   }
   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential, IHttpClientFactory httpClientFactory)
   {
      return new UserClientBuilder(userCredential, new NullLoggerFactory(), httpClientFactory);
   }

   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      return new UserClientBuilder(userCredential, loggerFactory, new DefaultHttpClientFactory());
   }

   public static UserClientBuilder CreateUserClientBuilder(UserCredential userCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      return new UserClientBuilder(userCredential, loggerFactory, httpClientFactory);
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential)
   {
      return new HybridClientBuilder(botCredential, userCredential, new NullLoggerFactory(), new DefaultHttpClientFactory());
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential, IHttpClientFactory httpClientFactory)
   {
      return new HybridClientBuilder(botCredential, userCredential, new NullLoggerFactory(), httpClientFactory);
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      return new HybridClientBuilder(botCredential, userCredential, loggerFactory, new DefaultHttpClientFactory());
   }

   public static HybridClientBuilder CreateHybridClientBuilder(BotCredential botCredential, UserCredential userCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      return new HybridClientBuilder(botCredential, userCredential, loggerFactory, httpClientFactory);
   }
}
