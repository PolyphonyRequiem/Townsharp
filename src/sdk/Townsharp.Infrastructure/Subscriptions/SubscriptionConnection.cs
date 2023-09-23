using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// Used to manage lifecycle of <see cref="SubscriptionClient"/> objects, including migrations and fault recovery.
/// Tracks subscriptions it is responsible for, used in recovery.
/// </remarks>
internal class SubscriptionConnection
{
    // Constants
    private const uint MAX_SUBSCRIPTION_RETRIES = 5;
    private const uint MAX_SUBSCRIPTIONS_AT_ONCE = 50;

    private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(105);
    // private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(2); // Testing only, James will hate you.
    private static TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    // State
    private readonly ConcurrentDictionary<SubscriptionDefinition, uint> ownedSubscriptions = new ConcurrentDictionary<SubscriptionDefinition, uint>();
    private readonly ConcurrentQueue<SubscriptionDefinition> pendingSubscriptions = new ConcurrentQueue<SubscriptionDefinition>();
    private readonly ConcurrentQueue<SubscriptionDefinition> pendingUnsubscriptions = new ConcurrentQueue<SubscriptionDefinition>();

    // Background Tasks
    private Task? workerTask;
    private Task? periodicMigrationTask;

    // State Transition Tasks
    private Task migratingTask = Task.CompletedTask;
    private Task recoveryTask = Task.CompletedTask;

    // Dependencies
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<SubscriptionConnection> logger;

    // Policies
    private static int[] RETRY_DELAYS_IN_SECONDS = new int[] { 0, 1, 5, 15, 30, 60};
    private readonly ResiliencePipeline<SubscriptionMessageClient> SubscriptionClientCreationRetryPolicy;

    public ConnectionId ConnectionId { get; init; }

    private readonly SubscriptionConnectionStateMachine stateMachine;

    internal SubscriptionConnection(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.ConnectionId = connectionId;
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<SubscriptionConnection>();
        this.stateMachine = new SubscriptionConnectionStateMachine();

        this.SubscriptionClientCreationRetryPolicy = new ResiliencePipelineBuilder<SubscriptionMessageClient>()
        .AddRetry(new RetryStrategyOptions<SubscriptionMessageClient>
        {
            ShouldHandle = new PredicateBuilder<SubscriptionMessageClient>().Handle<Exception>(),
            DelayGenerator = generatorArgs =>
            {
                int delayInSeconds = generatorArgs.AttemptNumber < RETRY_DELAYS_IN_SECONDS.Length
                    ? RETRY_DELAYS_IN_SECONDS[generatorArgs.AttemptNumber - 1]
                    : RETRY_DELAYS_IN_SECONDS[^1];

                if (generatorArgs.AttemptNumber != 1)
                {
                    this.logger.LogWarning($"An error occurred in {nameof(SubscriptionConnection)} while attempting to create a {nameof(SubscriptionMessageClient)}.  Retrying in {delayInSeconds}s");
                }

                TimeSpan? delay = TimeSpan.FromSeconds(delayInSeconds);

                return ValueTask.FromResult(delay);
            },
            MaxRetryAttempts = int.MaxValue,
            BackoffType = DelayBackoffType.Exponential
        })
        .Build();
    }

    internal async IAsyncEnumerable<SubscriptionEvent> ReceiveSubscriptionEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SubscriptionMessageClient? currentClient;
        SubscriptionMessageClient? migrationClient;

        MigrationToken migrationToken = MigrationToken.None;

        do
        {
            SubscriptionMessageClient? newClient = await this.CreateAndConnectNewClientAsync(cancellationToken);
            
            if (newClient == null)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.logger.LogCritical("We failed to create a new client, but the cancellation token was not requested.  This should never happen.");
                    throw new InvalidOperationException("Fatal error occurred while creating a new subscription client.");
                }
            }

            if (this.stateMachine.State == SubscriptionConnectionState.Created)
            {
                this.stateMachine.TransitionTo(SubscriptionConnectionState.ConnectedReady);
                currentClient = newClient;
            }

            if (this.stateMachine.State == SubscriptionConnectionState.MigrationTokenAcquired)
            {
                if (migrationToken == MigrationToken.None)
                {
                    this.logger.LogCritical("We are supposed to send a migration token, but the migration token is not available.  This should never happen.");
                    throw new InvalidOperationException("Fatal error occurred while migrating.  Migration token was not acquired but internal state indicates otherwise.");
                }

                var sendMigrationTokenResponse = await newClient!.SendMigrationTokenAsync(migrationToken.Token, MigrationTimeout, cancellationToken);

                if (!sendMigrationTokenResponse.IsCompleted)
                {
                    this.logger.LogError($"{this.ConnectionId} Send migration token operation did not complete in time. Error provided is '{sendMigrationTokenResponse.ErrorMessage}'.  Transitioning to Faulted.");
                    this.stateMachine.TransitionTo(SubscriptionConnectionState.Faulted);
                    continue;
                }
                else
                {
                    this.stateMachine.TransitionTo(SubscriptionConnectionState.ConnectedReady);
                    currentClient = newClient;
                }
            }

            migrationToken = MigrationToken.None; // We have attempted to consume it, so we should reset it.


            if (this.stateMachine.State == SubscriptionConnectionState.Faulted)
            {
                // initiate recovery
            }

            // The lifetime of newClient should be ended by this comment, so I probably need to refactor a bit.

            if (this.stateMachine.State == SubscriptionConnectionState.ConnectedReady)
            {
                currentClient = newClient;
                // we are ready to receive events and handle work!

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

            // do we have a migration token already?  
            // if so, let's try to migrate.
            // otherwise, start with a fresh client.
            // if we fail to migrate, we should try to recover.


            // Get a client
            SubscriptionMessageClient client = this.subscriptionClientFactory.CreateClient();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start processing work.
            //var isHandlingWork = true;

            //var workerTask = this.HandleSubscriptionWorkAsync(); // pass cancellation
            //var periodicMigrationTask = this.MigratePeriodically(); // shouldn't do this periodically. pass cancellation too.

            await foreach (var @event in client.ReceiveSubscriptionEventsAsync(cts.Token))
            {
                // emit each event.
                yield return @event;
            }

            var getMigrationTokenResult = await client.GetMigrationTokenAsync(MigrationTimeout);

            if (getMigrationTokenResult.IsCompleted)
            {
                // getMigrationTokenResult.Message.content
            }

            // this particular client is done handling work, but we should be moving on to the next one if we haven't cancelled.
        }
        while (!cancellationToken.IsCancellationRequested);
    }

    private async Task<SubscriptionMessageClient?> CreateAndConnectNewClientAsync(CancellationToken cancellationToken)
    {
        var result = await SubscriptionClientCreationRetryPolicy.ExecuteAsync(
            async _ =>
            {
                
                var client = this.subscriptionClientFactory.CreateClient();
                await client.ConnectAsync();
                return client;
            },
        cancellationToken);

        return result;
    }

    //private async Task MigratePeriodically()
    //{
    //    while (this.isHandlingWork)
    //    {
    //        await Task.Delay(MigrationFrequency);
    //        this.InitiateMigration();
    //        await this.migratingTask;
    //    }
    //}

    //private void InitiateMigration()
    //{
    //    if (this.disposed || !this.isHandlingWork || this.isMigrating || this.isRecovering)
    //    {
    //        return;
    //    }

    //    this.logger.LogTrace($"{this.ConnectionId} Initiating migration.");
    //    this.isMigrating = true;
    //    this.migratingTask = 
    //        this.TryMigrateAsync()
    //            .ContinueWith(async t =>
    //            {
    //                if (!t.IsCompletedSuccessfully || t.Result == false)
    //                {
    //                    this.InitiateRecovery();
    //                }

    //                this.isMigrating = false;
    //                await this.recoveryTask; // in case we fell into recovery.
    //            });
    //}

    //private async Task<bool> TryMigrateAsync()
    //{
    //    SubscriptionMessageClient oldClient = this.SubscriptionClient;

    //    // GET TOKEN
    //    this.logger.LogTrace("Getting Migration Token.");
    //    var getMigrationTokenResponse = await oldClient.GetMigrationTokenAsync(TimeSpan.FromSeconds(15));
    //    if (!getMigrationTokenResponse.IsCompleted)
    //    {
    //        this.logger.LogError($"{this.ConnectionId} Failed to get migration token.");
    //        return false;
    //    }
    //    var contentElement = JsonSerializer.Deserialize<JsonElement>(getMigrationTokenResponse.Message.content);

    //    if (!contentElement.TryGetProperty("token", out var tokenElement))
    //    {
    //        this.logger.LogError($"{this.ConnectionId} Get migration token response message did not contain token element.");
    //        return false;
    //    }
    //    this.logger.LogTrace("Got migration token.");

    //    // NEW CLIENT
    //    this.logger.LogTrace("Connecting new client.");
    //    SubscriptionMessageClient newClient = await this.CreateNewSubscriptionClientAsync();

    //    // SENT TOKEN
    //    this.logger.LogTrace("Sending Migration Token.");
    //    var sendMigrationTokenResponse = await newClient.SendMigrationTokenAsync(tokenElement.GetString()!, MigrationTimeout);
    //    if (!sendMigrationTokenResponse.IsCompleted)
    //    {
    //        this.logger.LogError($"{this.ConnectionId} Send migration token operation did not complete.");
    //        return false;
    //    }
        
    //    // CLEANUP
    //    this.logger.LogTrace("Finalizing Migration.");
    //    this.SubscriptionClient = newClient;
    //    await this.CleanupSubscriptionClientAsync(oldClient);
    //    return true;
    //}

    //private void InitiateRecovery()
    //{
    //    this.recoveryTask = Task.Run(this.RecoverConnectionAsync);
    //}

    //private async Task RecoverConnectionAsync()
    //{
    //    this.isRecovering = true;
    //    bool isRecovered = false;
    //    do
    //    {
    //        try
    //        {
    //            this.logger.LogWarning($"{this.ConnectionId} Recovering connection due to error.");
    //            // cleanup the old client
    //            var cleanupTask = this.CleanupSubscriptionClientAsync(this.SubscriptionClient);

    //            // connect the new one.
    //            this.SubscriptionClient = await this.CreateNewSubscriptionClientAsync();

    //            // all subscriptions were dropped, prepare for resubscription.
    //            this.pendingSubscriptions.Clear();
    //            this.pendingUnsubscriptions.Clear();

    //            foreach (var subscription in this.ownedSubscriptions.Keys)
    //            {
    //                this.pendingSubscriptions.Enqueue(subscription);
    //            }

    //            await cleanupTask;
                
    //            isRecovered = true;
    //            this.logger.LogWarning($"{this.ConnectionId} Recovered with {this.pendingSubscriptions.Count} subscriptions to recover.");
    //            this.isRecovering = false;
    //        }
    //        catch (Exception ex)
    //        {
    //            this.logger.LogError(ex, $"{this.ConnectionId} Failed to recover connection.");
    //            await Task.Delay(TimeSpan.FromSeconds(1));
    //        }
    //    } while (!this.disposed && isRecovered == false);

    //    this.isRecovering = false;
    //}

    //public void Subscribe(SubscriptionDefinition[] subscriptionDefinitions)
    //{
    //    // Remove duplicates from the subscription definitions.
    //    var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

    //    // Update the subscriptions and the work queue.
    //    foreach (var subscription in distinctSubscriptions)
    //    {
    //        if (this.ownedSubscriptions.TryAdd(subscription, 0))
    //        {
    //            this.pendingSubscriptions.Enqueue(subscription);
    //        }
    //        else
    //        {
    //            this.logger.LogWarning($"{this.ConnectionId} Ignoring duplicate subscription: {subscription}");
    //        }
    //    }
    //}

    //public void Unsubscribe(SubscriptionDefinition[] subscriptionDefinitions)
    //{
    //    // Remove duplicates from the subscription definitions.
    //    var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

    //    // Update the queue to include the new items.
    //    foreach (var subscription in distinctSubscriptions)
    //    {
    //        if (this.ownedSubscriptions.TryRemove(subscription, out _))
    //        {
    //            this.pendingUnsubscriptions.Enqueue(subscription);
    //        }
    //        else
    //        {
    //            this.logger.LogWarning($"{this.ConnectionId} Ignoring un-owned subscription: {subscription}");
    //        }
    //    }
    //}

    //private async Task HandleSubscriptionWorkAsync()
    //{
    //    bool idle = false;
    //    do
    //    {
    //        if (this.pendingSubscriptions.IsEmpty || !this.Ready)
    //        {
    //            await Task.Delay(TimeSpan.FromSeconds(1));
    //        }

    //        List<SubscriptionDefinition> subscriptionsTaken = new List<SubscriptionDefinition>();
    //        List<SubscriptionDefinition> unsubscriptionsTaken = new List<SubscriptionDefinition>();

    //        var subscriptionCount = this.pendingSubscriptions.Count;
    //        var unsubscriptionCount = this.pendingUnsubscriptions.Count;
    //        if (subscriptionCount != 0)
    //        {
    //            idle = false;
    //            this.logger.LogInformation($"ConnectionId {this.ConnectionId} has {subscriptionCount} pending subscriptions.  Handling up to {Math.Min(MAX_SUBSCRIPTIONS_AT_ONCE, subscriptionCount)} of them.");

    //            uint limit = MAX_SUBSCRIPTIONS_AT_ONCE;
    //            while (limit-- > 0 && !this.pendingSubscriptions.IsEmpty)
    //            {
    //                if (this.pendingSubscriptions.TryDequeue(out var subscription))
    //                {
    //                    subscriptionsTaken.Add(subscription);
    //                }
    //                else
    //                {
    //                    limit++;
    //                }
    //            }
    //        }
    //        else if (unsubscriptionCount != 0)
    //        {
    //            idle = false;
    //            this.logger.LogInformation($"ConnectionId {this.ConnectionId} has {subscriptionCount} pending unsubscriptions.  Handling up to {Math.Min(MAX_SUBSCRIPTIONS_AT_ONCE, subscriptionCount)} of them.");

    //            uint limit = MAX_SUBSCRIPTIONS_AT_ONCE;
    //            while (limit-- > 0 && !this.pendingUnsubscriptions.IsEmpty)
    //            {
    //                if (this.pendingUnsubscriptions.TryDequeue(out var subscription))
    //                {
    //                    unsubscriptionsTaken.Add(subscription);
    //                }
    //                else
    //                {
    //                    limit++;
    //                }
    //            }
    //        }
    //        else if (!idle)
    //        {
    //            idle = true;
    //            this.logger.LogInformation($"ConnectionId {this.ConnectionId} has resolved all pending subscriptions and the worker is Idle.");
    //        }

    //        Task[] subscriptionTasks = subscriptionsTaken.Select(this.SendSubscriptionRequestAsync).ToArray();

    //        if (subscriptionTasks.Any())
    //        {
    //            Task.WaitAll(subscriptionTasks);
    //        }

    //        Task[] unsubscriptionTasks = unsubscriptionsTaken.Select(this.SendUnsubscriptionRequestAsync).ToArray();

    //        if (unsubscriptionTasks.Any())
    //        {
    //            Task.WaitAll(unsubscriptionTasks);
    //        }
    //    }
    //    while (this.isHandlingWork);
    //}

    //private Task SendSubscriptionRequestAsync(SubscriptionDefinition definition)
    //{
    //    if (!this.ownedSubscriptions.ContainsKey(definition))
    //    {
    //        this.logger.LogWarning($"{this.ConnectionId} Ignoring subscription request for unowned subscription: {definition}");
    //        return Task.CompletedTask;
    //    }

    //    // Note, we no inter have the notion of an error response etc.  We will need to handle the message ourselves.
    //    var response = this.SubscriptionClient.SubscribeAsync(definition.EventId, definition.KeyId, TimeSpan.FromSeconds(15))
    //        .ContinueWith(task =>
    //        {
    //            // handle result.
    //            if (task.IsFaulted)
    //            {
    //                // handle error.
    //                this.logger.LogError($"{this.ConnectionId} Subscription request task faulted.");
    //                TryRequeueSubscriptionRequest(definition);
    //            }

    //            if (task.IsCanceled)
    //            {
    //                // handle cancel.
    //                Debugger.Break();
    //            }

    //            if (task.IsCompleted)
    //            {
    //                var result = task.Result;
    //                if (result.TimedOut)
    //                {
    //                    // handle failure.
    //                    this.logger.LogWarning($"{this.ConnectionId} Subscription request timed out.");
    //                    TryRequeueSubscriptionRequest(definition);
    //                }

    //                if (!string.IsNullOrEmpty(result.ErrorMessage))
    //                {
    //                    // handle error.
    //                    this.logger.LogError($"{this.ConnectionId} Subscription request failed {result.ErrorMessage}");
    //                    TryRequeueSubscriptionRequest(definition);
    //                }

    //                if (result.IsCompleted)
    //                {
    //                    // handle success.
    //                }
    //            }

    //            return task.Result;
    //        });

    //    return response;
    //}

    //private Task SendUnsubscriptionRequestAsync(SubscriptionDefinition definition)
    //{
    //    if (this.ownedSubscriptions.ContainsKey(definition))
    //    {
    //        this.logger.LogWarning($"{this.ConnectionId} Ignoring unsubscription request for owned subscription: {definition}");
    //        return Task.CompletedTask;
    //    }

    //    // Note, we no inter have the notion of an error response etc.  We will need to handle the message ourselves.
    //    var response = this.SubscriptionClient.UnsubscribeAsync(definition.EventId, definition.KeyId, TimeSpan.FromSeconds(15))
    //        .ContinueWith(task =>
    //        {
    //            // handle result.
    //            if (task.IsFaulted)
    //            {
    //                // handle error.
    //                this.logger.LogError($"{this.ConnectionId} Unsubscription request task faulted.");
    //            }

    //            if (task.IsCanceled)
    //            {
    //                // handle cancel.
    //                Debugger.Break();
    //            }

    //            if (task.IsCompleted)
    //            {
    //                var result = task.Result;
    //                if (result.TimedOut)
    //                {
    //                    // handle failure.
    //                    this.logger.LogWarning($"{this.ConnectionId} Unsubscription request timed out.");
    //                }

    //                if (!string.IsNullOrEmpty(result.ErrorMessage))
    //                {
    //                    // handle error.
    //                    this.logger.LogError($"{this.ConnectionId} Unsubscription request failed {result.ErrorMessage}");
    //                }

    //                if (result.IsCompleted)
    //                {
    //                    // handle success.
    //                }
    //            }

    //            return task.Result;
    //        });

    //    return response;
    //}

    //private void TryRequeueSubscriptionRequest(SubscriptionDefinition definition)
    //{
    //    do
    //    {
    //        if (this.ownedSubscriptions.TryGetValue(definition, out var retryCount))
    //        {
    //            if (retryCount < MAX_SUBSCRIPTION_RETRIES)
    //            {
    //                if (this.ownedSubscriptions.TryUpdate(definition, 0, retryCount))
    //                {
    //                    this.pendingSubscriptions.Enqueue(definition);
    //                    break;
    //                }
    //            }
    //            else
    //            {
    //                this.logger.LogWarning($"{this.ConnectionId} Subscription request for {definition} failed too many times.  Removing subscription.");
    //            }
    //        }
    //        else
    //        {
    //            break;
    //        }
    //    } while (true);
    //}

    //////////////////////
    //// Disposal
    //////////////////////
    //protected virtual void Dispose(bool disposing)
    //{
    //    if (!this.disposed)
    //    {
    //        if (disposing)
    //        {
    //            // TODO: dispose managed state (managed objects)
    //        }

    //        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
    //        // TODO: set large fields to null
    //        this.disposed = true;
    //    }
    //}

    //public void Dispose()
    //{
    //    this.Dispose(disposing: true);
    //    GC.SuppressFinalize(this);
    //}

    //public async ValueTask DisposeAsync()
    //{
    //    await this.DisposeAsyncCore().ConfigureAwait(false);
    //    this.Dispose(disposing: false);
    //    GC.SuppressFinalize(this);
    //}

    //protected virtual async ValueTask DisposeAsyncCore()
    //{
    //    this.isHandlingWork = false;

    //    await Task.WhenAll(
    //        this.workerTask ?? Task.CompletedTask,
    //        this.periodicMigrationTask ?? Task.CompletedTask);

    //    if (this.subscriptionClient != null)
    //    {
    //        this.subscriptionClient.EventReceived -= this.HandleSubscriptionEvent;
    //        this.subscriptionClient.Disconnected -= this.HandleOnWebsocketFaulted;
    //        await this.subscriptionClient.DisconnectAsync();
    //    }
    //}
}