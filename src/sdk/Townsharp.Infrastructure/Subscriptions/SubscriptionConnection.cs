using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using Townsharp.Infrastructure.Subscriptions.Models;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// Used to manage lifecycle of <see cref="SubscriptionWebsocketClient"/> objects, including migrations and fault recovery.
/// Tracks subscriptions it is responsible for, used in recovery.
/// </remarks>
internal partial class SubscriptionConnection
{
   // Constants
   private const int MAX_SUBSCRIPTIONS_AT_ONCE = 50;

   private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(105);
   // private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(2); // Testing only, James will hate you.
   private static TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

   // Dependencies
   private readonly SubscriptionClientFactory subscriptionClientFactory;
   private readonly ILoggerFactory loggerFactory;
   private readonly ILogger<SubscriptionConnection> logger;

   // Policies
   private static int[] RETRY_DELAYS_IN_SECONDS = new int[] { 0, 1, 5, 15, 30, 60 };
   private readonly ResiliencePipeline<SubscriptionWebsocketClient> SubscriptionClientCreationRetryPolicy;
   private readonly SubscriptionWorkTracker workTracker;

   internal ConnectionId ConnectionId { get; init; }

   private SubscriptionConnectionState connectionState;

   private Task workerTask = Task.CompletedTask;
   private Task migrationDueTask = Task.CompletedTask;

   private Channel<SubscriptionEvent> eventChannel = Channel.CreateUnbounded<SubscriptionEvent>(
       new UnboundedChannelOptions
       {
          AllowSynchronousContinuations = true,
          SingleReader = true,
          SingleWriter = true
       });

   internal SubscriptionConnection(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
   {
      this.ConnectionId = connectionId;
      this.subscriptionClientFactory = subscriptionClientFactory;
      this.loggerFactory = loggerFactory;
      this.logger = loggerFactory.CreateLogger<SubscriptionConnection>();
      this.connectionState = SubscriptionConnectionState.NotConnected;
      this.workTracker = new SubscriptionWorkTracker(loggerFactory.CreateLogger<SubscriptionWorkTracker>());

      this.LogCreatedSubscriptionConnection(this.ConnectionId);

      this.SubscriptionClientCreationRetryPolicy = new ResiliencePipelineBuilder<SubscriptionWebsocketClient>()
          .AddRetry(new RetryStrategyOptions<SubscriptionWebsocketClient>
          {
             ShouldHandle = new PredicateBuilder<SubscriptionWebsocketClient>().Handle<Exception>(),
             DelayGenerator = generatorArgs =>
               {
                 int delayInSeconds = generatorArgs.AttemptNumber < RETRY_DELAYS_IN_SECONDS.Length
                       ? RETRY_DELAYS_IN_SECONDS[generatorArgs.AttemptNumber - 1]
                       : RETRY_DELAYS_IN_SECONDS[^1];

                 if (generatorArgs.AttemptNumber != 1)
                 {
                    this.LogRetryableErrorCreatingSubscriptionClient(this.ConnectionId, delayInSeconds);
                 }

                 TimeSpan? delay = TimeSpan.FromSeconds(delayInSeconds);

                 return ValueTask.FromResult(delay);
              },
             MaxRetryAttempts = int.MaxValue,
             BackoffType = DelayBackoffType.Exponential
          })
          .Build();
   }

   //create client
   //        send token if present ?
   //            close old client | initiate and wait for recovery-> throw recovered event.
   //	      get events
   //        send pending requests
   //        ---
   //        Time to Migrate | Cancel -> cleanup.
   //        Get Token?
   //        |   success -> set token for next iteration
   //        |   fail -> cleanup old client
   //loop  <-/

   internal IAsyncEnumerable<SubscriptionEvent> ReadAllEventsAsync(CancellationToken cancellationToken)
       => this.eventChannel.Reader.ReadAllAsync(cancellationToken);

   internal async Task RunAsync(CancellationToken cancellationToken)
   {
      this.LogStartingSubscriptionConnection(this.ConnectionId);
      SubscriptionWebsocketClient? currentClient = default;
      CancellationTokenSource currentClientCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      MigrationToken migrationToken = MigrationToken.None;

      do
      {
         CancellationTokenSource newClientCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
         SubscriptionWebsocketClient? newClient = await this.CreateAndConnectNewClientAsync(newClientCancellationTokenSource.Token);

         if (newClient == null)
         {
            if (!cancellationToken.IsCancellationRequested)
            {
               this.LogCreateClientFailedWithoutCancellation(this.ConnectionId);  // do we need this if we are throwing?
               throw new InvalidOperationException("Fatal error occurred while creating a new subscription client.");
            }
         }

         if (this.connectionState == SubscriptionConnectionState.NotConnected)
         {
            this.connectionState = SubscriptionConnectionState.ConnectedReady;
            currentClient = newClient;
            currentClientCancellationTokenSource = newClientCancellationTokenSource;
         }

         if (this.connectionState == SubscriptionConnectionState.MigrationTokenAcquired)
         {
            if (migrationToken == MigrationToken.None)
            {
               this.LogMigrationFailedDueToNoToken(this.ConnectionId);  // do we need this if we are throwing?
               throw new InvalidOperationException("Fatal error occurred while migrating.  Migration token was not acquired but internal state indicates otherwise.");
            }

            var sendMigrationTokenResponse = await newClient!.SendMigrationTokenAsync(migrationToken.Token, MigrationTimeout, cancellationToken);

            // clean up current client
            if (!currentClientCancellationTokenSource.IsCancellationRequested)
            {
               currentClientCancellationTokenSource.Cancel();
            }
            currentClient = null;

            if (!sendMigrationTokenResponse.IsCompleted)
            {
               this.LogMigrationTokenSendFailed(this.ConnectionId, sendMigrationTokenResponse.ErrorMessage);
               this.connectionState = SubscriptionConnectionState.Faulted;

               // clean up new client
               newClientCancellationTokenSource.Cancel();
               continue;
            }
            else
            {
               this.connectionState = SubscriptionConnectionState.ConnectedReady;
               currentClient = newClient;
               currentClientCancellationTokenSource = newClientCancellationTokenSource;
            }
         }

         migrationToken = MigrationToken.None; // We have attempted to consume it, so we should reset it.

         if (this.connectionState == SubscriptionConnectionState.Faulted)
         {
            // initiate recovery
            this.workTracker.ResetDispositionsForRecovery();
            this.connectionState = SubscriptionConnectionState.NotConnected;
            continue;
         }

         // The lifetime of newClient should be ended by this comment, so I probably need to refactor a bit.

         // Okay, if we faulted, we are now clear to recover
         // Let's handle work, and emit events.

         // should only be connected ready, otherwise something went weird.
         if (this.connectionState == SubscriptionConnectionState.ConnectedReady)
         {
            if (currentClient == null)
            {
               throw new InvalidOperationException("currentClient should not be able to be null here");
            }

            // set the migration due time.
            this.migrationDueTask = Task.Delay(MigrationFrequency);

            // handle work until it's time to migrate.
            do
            {
               var leases = this.workTracker.TakeWorkLeases(MAX_SUBSCRIPTIONS_AT_ONCE);

               if (leases.Length == 0)
               {
                  this.LogNoWork(this.ConnectionId);
                  this.workerTask = Task.Delay(TimeSpan.FromSeconds(5)); // time to snooze
               }
               else
               {
                  this.LogLeaseWork(this.ConnectionId, leases.Length);
                  this.workerTask = this.ProcessLeasesAsync(currentClient, leases);
               }

               await Task.WhenAny(this.workerTask, this.migrationDueTask);
            }
            while (!currentClientCancellationTokenSource.Token.IsCancellationRequested && !this.migrationDueTask.IsCompleted);

            var getMigrationTokenResult = await currentClient!.GetMigrationTokenAsync(MigrationTimeout);
            if (getMigrationTokenResult.IsCompleted)
            {
               migrationToken = MigrationToken.FromContent(getMigrationTokenResult!.Message!.content);
               this.connectionState = SubscriptionConnectionState.MigrationTokenAcquired;
            }
            else
            {
               migrationToken = MigrationToken.None;
               this.connectionState = SubscriptionConnectionState.Faulted;
            }
         }
         // this particular client is done handling work, but we should be moving on to the next one if we haven't cancelled.
      }
      while (!cancellationToken.IsCancellationRequested);
   }

   private async Task ProcessLeasesAsync(SubscriptionWebsocketClient client, SubscriptionWorkLease[] leases)
   {
      await ProcessLeasesForIntentAsync(client, leases, SubscriptionIntent.Subscribed);
      await ProcessLeasesForIntentAsync(client, leases, SubscriptionIntent.Unsubscribed);
   }

   private async Task ProcessLeasesForIntentAsync(SubscriptionWebsocketClient client, SubscriptionWorkLease[] leases, SubscriptionIntent intent)
   {
      var workTasks = new List<Task>();

      foreach (var lease in leases)
      {
         if (lease.Intent == intent)
         {
            if (lease.PriorDisposition == SubscriptionDisposition.RetryNeeded)
            {
               this.LogProcessLeaseForIntentRetryNeeded(this.ConnectionId, intent, lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId);
            }
            else
            {
               this.LogProcessLeaseForIntent(this.ConnectionId, intent, lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId);
            }
            workTasks.Add(this.RequestResolutionForIntentAsync(lease, intent == SubscriptionIntent.Subscribed ? client.SubscribeAsync : client.UnsubscribeAsync));
         }
      }

      await Task.WhenAll(workTasks.ToArray());
   }

   private async Task RequestResolutionForIntentAsync(SubscriptionWorkLease lease, Func<string, int, TimeSpan, Task<Response<SubscriptionResponseMessage>>> sendClientRequestAsync)
   {
      var response = await sendClientRequestAsync(lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId, TimeSpan.FromSeconds(15));

      if (response.IsCompleted)
      {
         this.LogSubscriptionSucceeded(this.ConnectionId, lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId);
         this.workTracker.ReportLeaseResolved(lease);
      }
      else if (response.TimedOut)
      {
         this.LogSubscriptionTimedOut(this.ConnectionId, lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId);
         this.workTracker.ReportLeaseRetryNeeded(lease);
      }
      else
      {
         this.LogSubscriptionFailedWithError(this.ConnectionId, lease.SubscriptionDefinition.EventId, lease.SubscriptionDefinition.KeyId, response.ErrorMessage);
      }
   }

   private async Task<SubscriptionWebsocketClient?> CreateAndConnectNewClientAsync(CancellationToken cancellationToken)
   {
      var result = await SubscriptionClientCreationRetryPolicy.ExecuteAsync(
          async _ =>
          {

             var client = this.subscriptionClientFactory.CreateClient(this.eventChannel.Writer);
             await client.ConnectAsync(cancellationToken);
             return client;
          },
      cancellationToken);

      return result;
   }

   internal void Subscribe(SubscriptionDefinition[] subscriptions)
   {
      this.workTracker.AddSubscriptions(subscriptions);
      this.LogUpdatedSubscriptions(this.ConnectionId, subscriptions.Length);
   }

   internal void Unsubscribe(SubscriptionDefinition[] unsubscriptions)
   {
      this.workTracker.AddUnsubscriptions(unsubscriptions);
      this.LogUpdatedUnsubscriptions(this.ConnectionId, unsubscriptions.Length);
   }

   [LoggerMessage(EventId = 2111, Message = "{connectionId} - Created SubscriptionConnection.", Level = LogLevel.Debug)]
   internal partial void LogCreatedSubscriptionConnection(ConnectionId connectionId);

   [LoggerMessage(EventId = 2112, Message = "{connectionId} - Starting subscription connection.", Level = LogLevel.Debug)]
   internal partial void LogStartingSubscriptionConnection(ConnectionId connectionId);

   [LoggerMessage(EventId = 2113, Message = "{connectionId} - No lease work, sleeping.", Level = LogLevel.Debug)]
   internal partial void LogNoWork(ConnectionId connectionId);

   [LoggerMessage(EventId = 2114, Message = "{connectionId} - Found lease work for {count} operations. Reconciling.", Level = LogLevel.Debug)]
   internal partial void LogLeaseWork(ConnectionId connectionId, int count);

   [LoggerMessage(EventId = 2115, Message = "{connectionId} - Retrying attempt to reconcile lease for intent {intent} - {eventId}/{keyId}.", Level = LogLevel.Debug)]
   internal partial void LogProcessLeaseForIntentRetryNeeded(ConnectionId connectionId, SubscriptionIntent intent, string eventId, int keyId);

   [LoggerMessage(EventId = 2116, Message = "{connectionId} - Attempting to reconcile lease for intent {intent} - {eventId}/{keyId}.", Level = LogLevel.Debug)]
   internal partial void LogProcessLeaseForIntent(ConnectionId connectionId, SubscriptionIntent intent, string eventId, int keyId);

   [LoggerMessage(EventId = 2117, Message = "{connectionId} - Subscription {eventId}/{keyId} succeeded.", Level = LogLevel.Debug)]
   internal partial void LogSubscriptionSucceeded(ConnectionId connectionId, string eventId, int keyId);

   [LoggerMessage(EventId = 2118, Message = "{connectionId} - Subscription {eventId}/{keyId} timed out.", Level = LogLevel.Debug)]
   internal partial void LogSubscriptionTimedOut(ConnectionId connectionId, string eventId, int keyId);

   [LoggerMessage(EventId = 2119, Message = "{connectionId} - Subscription {eventId}/{keyId} failed with error {error}.", Level = LogLevel.Debug)]
   internal partial void LogSubscriptionFailedWithError(ConnectionId connectionId, string eventId, int keyId, string error);

   [LoggerMessage(EventId = 3111, Message = "{connectionId} - Updated {count} subscriptions.", Level = LogLevel.Information)]
   internal partial void LogUpdatedSubscriptions(ConnectionId connectionId, int count);

   [LoggerMessage(EventId = 3112, Message = "{connectionId} - Updated {count} unsubscriptions.", Level = LogLevel.Information)]
   internal partial void LogUpdatedUnsubscriptions(ConnectionId connectionId, int count);

   [LoggerMessage(EventId = 5111, Message = "{connectionId} - A retryable error occurred in SubscriptionConnection while attempting to create a SubscriptionClient.  Retrying in {delayInSeconds}s", Level = LogLevel.Warning)]
   internal partial void LogRetryableErrorCreatingSubscriptionClient(ConnectionId connectionId, int delayInSeconds);

   [LoggerMessage(EventId = 6111, Message = "{connectionId} - We failed to create a new client, but the cancellation token was not requested.  This should never happen.", Level = LogLevel.Critical)]
   internal partial void LogCreateClientFailedWithoutCancellation(ConnectionId connectionId);

   [LoggerMessage(EventId = 6112, Message = "{connectionId} - We are supposed to send a migration token, but the migration token is not available.  This should never happen.", Level = LogLevel.Critical)]
   internal partial void LogMigrationFailedDueToNoToken(ConnectionId connectionId);

   [LoggerMessage(EventId = 6113, Message = "{connectionId} - Send migration token operation did not complete in time. Error provided is '{errorMessage}'.  Transitioning to Faulted.", Level = LogLevel.Critical)]
   internal partial void LogMigrationTokenSendFailed(ConnectionId connectionId, string errorMessage);
}

internal enum SubscriptionConnectionState
{
   NotConnected,
   MigrationTokenAcquired,
   Faulted,
   ConnectedReady
}
