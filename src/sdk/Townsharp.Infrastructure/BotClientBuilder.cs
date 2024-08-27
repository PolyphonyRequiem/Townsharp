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
   
   internal protected BotClientBuilder(BotCredential botCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      this.botCredential = botCredential;
      this.loggerFactory = loggerFactory;
      this.httpClientFactory = httpClientFactory;

      this.botTokenProvider = new BotTokenProvider(this.botCredential, this.httpClientFactory, this.loggerFactory.CreateLogger<BotTokenProvider>());
      this.subscriptionClientFactory = new SubscriptionClientFactory(this.botTokenProvider, this.loggerFactory);
   }

   public ISubscriptionClient BuildSubscriptionClient(int concurrentConnections = 1) 
      => SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);

   public WebApiBotClient BuildWebApiClient() 
      => new WebApiBotClient(this.botTokenProvider, this.httpClientFactory, this.loggerFactory.CreateLogger<WebApiBotClient>());

   public IConsoleClient BuildConsoleClient(IWebApiClient webApiClient, int serverId)
   {
      var result = webApiClient.RequestConsoleAccessAsync(serverId).Result;

      if (!result.IsSuccess || !result.Content.IndicatesAccessGranted)
      {
         throw new UnauthorizedAccessException($"Failed to obtain console access. {result.ErrorMessage ?? "Access Denied"}");
      }

      return new ConsoleWebsocketClient(result.Content.BuildConsoleUri(), result.Content.token!, this.loggerFactory.CreateLogger<ConsoleWebsocketClient>());
   }
}
