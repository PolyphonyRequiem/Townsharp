using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure;

public class HybridClientBuilder
{
   private readonly BotCredential botCredential;
   private readonly UserCredential userCredential;
   private readonly ILoggerFactory loggerFactory;
   private readonly IHttpClientFactory httpClientFactory;
   private readonly BotTokenProvider botTokenProvider;
   private readonly UserTokenProvider userTokenProvider;
   private readonly SubscriptionClientFactory subscriptionClientFactory;

   internal protected HybridClientBuilder(BotCredential botCredential, UserCredential userCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      this.botCredential = botCredential;
      this.userCredential = userCredential;
      this.loggerFactory = loggerFactory;
      this.httpClientFactory = httpClientFactory;

      this.botTokenProvider = new BotTokenProvider(this.botCredential, this.httpClientFactory, this.loggerFactory.CreateLogger<BotTokenProvider>());
      this.userTokenProvider = new UserTokenProvider(this.userCredential, this.httpClientFactory, this.loggerFactory.CreateLogger<UserTokenProvider>());
      this.subscriptionClientFactory = new SubscriptionClientFactory(this.botTokenProvider, this.loggerFactory);
   }

   public ISubscriptionClient BuildSubscriptionClient(int concurrentConnections = 1)
      => SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);

   public WebApiUserClient BuildWebApiUserClient()
      => new WebApiUserClient(this.userTokenProvider, this.httpClientFactory, this.loggerFactory.CreateLogger<WebApiUserClient>());

   public WebApiBotClient BuildWebApiBotClient()
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