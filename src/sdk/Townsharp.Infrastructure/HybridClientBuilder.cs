using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions;

namespace Townsharp.Infrastructure;

public class HybridClientBuilder
{
   private readonly BotCredential botCredential;
   private readonly UserCredential userCredential;
   private readonly ILoggerFactory loggerFactory;

   private readonly BotTokenProvider botTokenProvider;
   private readonly SubscriptionClientFactory subscriptionClientFactory;

   internal protected HybridClientBuilder(BotCredential botCredential, UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      this.botCredential = botCredential;
      this.userCredential = userCredential;
      this.loggerFactory = loggerFactory;

      this.botTokenProvider = new BotTokenProvider(this.botCredential);
      this.subscriptionClientFactory = new SubscriptionClientFactory(this.botTokenProvider, this.loggerFactory);
   }

   public ISubscriptionClient BuildSubscriptionClient(int concurrentConnections = 1)
      => SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);
}