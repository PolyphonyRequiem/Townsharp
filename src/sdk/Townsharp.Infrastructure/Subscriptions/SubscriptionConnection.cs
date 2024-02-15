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
/// Used to manage lifecycle of <see cref="SubscriptionClient"/> objects, including migrations and fault recovery.
/// Tracks subscriptions it is responsible for, used in recovery.
/// </remarks>
internal class SubscriptionConnection
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
    private readonly ResiliencePipeline<SubscriptionClient> SubscriptionClientCreationRetryPolicy;
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

        this.logger.LogInformation($"Created SubscriptionConnection with connectionId {connectionId}.");

        this.SubscriptionClientCreationRetryPolicy = new ResiliencePipelineBuilder<SubscriptionClient>()
            .AddRetry(new RetryStrategyOptions<SubscriptionClient>
            {
                ShouldHandle = new PredicateBuilder<SubscriptionClient>().Handle<Exception>(),
                DelayGenerator = generatorArgs =>
                {
                    int delayInSeconds = generatorArgs.AttemptNumber < RETRY_DELAYS_IN_SECONDS.Length
                        ? RETRY_DELAYS_IN_SECONDS[generatorArgs.AttemptNumber - 1]
                        : RETRY_DELAYS_IN_SECONDS[^1];

                    if (generatorArgs.AttemptNumber != 1)
                    {
                        this.logger.LogWarning($"An error occurred in {nameof(SubscriptionConnection)} while attempting to create a {nameof(SubscriptionClient)}.  Retrying in {delayInSeconds}s");
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
        this.logger.LogInformation($"Starting subscription connection {this.ConnectionId}.");
        SubscriptionClient? currentClient = default;
        CancellationTokenSource currentClientCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        MigrationToken migrationToken = MigrationToken.None;

        do
        {
            CancellationTokenSource newClientCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SubscriptionClient? newClient = await this.CreateAndConnectNewClientAsync(newClientCancellationTokenSource.Token);

            if (newClient == null)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.logger.LogCritical("We failed to create a new client, but the cancellation token was not requested.  This should never happen.");
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
                    this.logger.LogCritical("We are supposed to send a migration token, but the migration token is not available.  This should never happen.");
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
                    this.logger.LogError($"{this.ConnectionId} Send migration token operation did not complete in time. Error provided is '{sendMigrationTokenResponse.ErrorMessage}'.  Transitioning to Faulted.");
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
                        this.logger.LogInformation($"{ConnectionId} has no lease work. Sleeping.");
                        this.workerTask = Task.Delay(TimeSpan.FromSeconds(5)); // time to snooze
                    }
                    else
                    {
                        this.logger.LogInformation($"{ConnectionId} has leases for {leases.Length} subscriptions.");
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

    private async Task ProcessLeasesAsync(SubscriptionClient client, SubscriptionWorkLease[] leases)
    {
        await ProcessLeasesForIntentAsync(client, leases, SubscriptionIntent.Subscribed);
        await ProcessLeasesForIntentAsync(client, leases, SubscriptionIntent.Unsubscribed);
    }

    private async Task ProcessLeasesForIntentAsync(SubscriptionClient client, SubscriptionWorkLease[] leases, SubscriptionIntent intent)
    {
        var workTasks = new List<Task>();

        foreach (var lease in leases)
        {
            if (lease.Intent == intent)
            {
                if (lease.PriorDisposition == SubscriptionDisposition.RetryNeeded)
                {
                    this.logger.LogDebug($"Retrying attempt to reconcile lease for intent {intent} - {lease.SubscriptionDefinition.EventId}/{lease.SubscriptionDefinition.KeyId}.");
                }
                else
                {
                    this.logger.LogDebug($"Attempting to reconcile lease for intent {intent} - {lease.SubscriptionDefinition.EventId}/{lease.SubscriptionDefinition.KeyId}.");
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
            this.logger.LogDebug($"Subscription {lease.SubscriptionDefinition.EventId}/{lease.SubscriptionDefinition.KeyId} succeeded.");
            this.workTracker.ReportLeaseResolved(lease);
        }
        else if (response.TimedOut)
        {
            this.logger.LogDebug($"Subscription {lease.SubscriptionDefinition.EventId}/{lease.SubscriptionDefinition.KeyId} timed out.");
            this.workTracker.ReportLeaseRetryNeeded(lease);
        }
        else
        {
            this.logger.LogDebug($"Subscription {lease.SubscriptionDefinition.EventId}/{lease.SubscriptionDefinition.KeyId} failed with error {response.ErrorMessage}.");
        }
    }

    private async Task<SubscriptionClient?> CreateAndConnectNewClientAsync(CancellationToken cancellationToken)
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
        this.logger.LogInformation($"{ConnectionId} has updated {subscriptions.Length} subscriptions.");
    }

    internal void Unsubscribe(SubscriptionDefinition[] unsubscriptions)
    {
        this.workTracker.AddUnsubscriptions(unsubscriptions);
        this.logger.LogInformation($"{ConnectionId} has updated {unsubscriptions.Length} unsubscriptions.");
    }
}

internal enum SubscriptionConnectionState
{
    NotConnected,
    MigrationTokenAcquired,
    Faulted,
    ConnectedReady
}