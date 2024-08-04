using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Subscriptions.Models;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Client;

public class BotSession
{
   private readonly BotCredential credential;
   private readonly BotClientSessionConfiguration configuration;

   private readonly IHttpClientFactory httpClientFactory;
   private readonly ILoggerFactory loggerFactory;

   private readonly WebApiBotClient webApiClient;
   private readonly SubscriptionMultiplexer subscriptionMultiplexer;
   private readonly ConsoleClientFactory consoleClientFactory;
   private readonly ILogger<BotSession> logger;
   private CancellationTokenSource? cancellationTokenSource;

   private Action<ManagedServer> handleServerAdded = _ => { };

   public void HandleServerAdded(Action<ManagedServer> action) => this.handleServerAdded = action;

   private Action<ManagedServer> handleServerRemoved = _ => { };

   public void HandleServerRemoved(Action<ManagedServer> action) => this.handleServerRemoved = action;

   private Action<ManagedGroup> handleGroupAdded = _ => { };

   public void HandleGroupAdded(Action<ManagedGroup> action) => this.handleGroupAdded = action;

   private Action<ManagedGroup> handleGroupRemoved = _ => { };

   public void HandleGroupRemoved(Action<ManagedGroup> action) => this.handleGroupRemoved = action;

   // should probably move half of these out into the factory.
   internal BotSession(
       BotCredential credential,
       BotClientSessionConfiguration configuration,
       IHttpClientFactory httpClientFactory,
       ILoggerFactory loggerFactory)
   {
      this.credential = credential;
      this.configuration = configuration;
      this.httpClientFactory = httpClientFactory;
      this.loggerFactory = loggerFactory;

      var subscriptionMultiplexerFactory = new SubscriptionMultiplexerFactory(credential); // nope, we should use builder extensions here, and this is why we kept those!

      this.webApiClient = new WebApiBotClient(credential);
      this.subscriptionMultiplexer = subscriptionMultiplexerFactory.CreateSubscriptionClient(this.configuration.MaxSubscriptionWebsockets);
      this.subscriptionMultiplexer.SubscriptionEventReceived += this.SubscriptionMultiplexer_SubscriptionEventReceived;
      this.consoleClientFactory = new ConsoleClientFactory(loggerFactory);
      this.logger = this.loggerFactory.CreateLogger<BotSession>();
   }

   private void SubscriptionMultiplexer_SubscriptionEventReceived(object? sender, SubscriptionEvent e)
   {
      this.logger.LogTrace(e.ToString());

      if (e is InvitedToGroupEvent invite)
      {
         this.logger.LogInformation($"Received group invite to {invite.Content.name}-{invite.Content.id}");
         // TODO: handle this async with a dedicated service.
         _ = this.webApiClient.AcceptGroupInviteAsync(invite.Content.id);
      }
   }

   public async Task RunAsync(CancellationToken cancellationToken = default)
   {
      this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      var subscriptionMultiplexerTask = this.subscriptionMultiplexer.RunAsync(this.cancellationTokenSource.Token);
      if (this.configuration.AutoAcceptInvites)
      {
         // Set up Auto Accept Invites
         this.subscriptionMultiplexer.RegisterSubscriptions([new SubscriptionDefinition("me-group-invite-create", 80634787)]); // we have a gap here, need a better way to expose the bot identity
         await this.webApiClient.GetPendingGroupInvitesAsync().ContinueWith(
            async task =>
            {
               var invites = await task;
               foreach (var invite in invites)
               {
                  await this.webApiClient.AcceptGroupInviteAsync(invite.id);
               }
            });
      }

      if (this.configuration.AutoManageGroupsAndServers)
      {
         // Setup AutomanageGroupsAndServers
         // await management complete.
      }

      await subscriptionMultiplexerTask;
   }
}