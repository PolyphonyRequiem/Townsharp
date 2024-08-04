using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure;

public class BotClientBuilder
{
   private readonly BotCredential botCredential;
   private readonly ILoggerFactory loggerFactory;
   private readonly IHttpClientFactory httpClientFactory;
   private readonly BotTokenProvider botTokenProvider;
   private readonly SubscriptionClientFactory subscriptionClientFactory;
   private readonly ConsoleClientFactory consoleClientFactory;

   internal protected BotClientBuilder(BotCredential botCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      this.botCredential = botCredential;
      this.loggerFactory = loggerFactory;
      this.httpClientFactory = httpClientFactory;

      this.botTokenProvider = new BotTokenProvider(this.botCredential, this.httpClientFactory, this.loggerFactory.CreateLogger<BotTokenProvider>());
      this.subscriptionClientFactory = new SubscriptionClientFactory(this.botTokenProvider, this.loggerFactory);
      this.consoleClientFactory = new ConsoleClientFactory(loggerFactory);
   }

   public ISubscriptionClient BuildSubscriptionClient(int concurrentConnections = 1) 
      => SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);

   public WebApiBotClient BuildWebApiClient() 
      => new WebApiBotClient(this.botTokenProvider, this.httpClientFactory, this.loggerFactory.CreateLogger<WebApiBotClient>());

   public IConsoleClient BuildConsoleClient()
   {

      this.consoleClientFactory.CreateClient();
   }

   // We should actually build the builder based on our credentials model, which informs which webapi client we build and what clients we can produce
}
