using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// Used to manage lifecycle of <see cref="SubscriptionClient"/> objects, including migrations and fault recovery.
/// Tracks subscriptions it is responsible for, used in recovery.
/// </remarks>
public class SubscriptionConnection : IDisposable, IAsyncDisposable
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

    private bool disposed;
    private bool isMigrating;
    private bool isRecovering;
    private bool isHandlingWork;

    private bool Ready => !this.disposed && this.SubscriptionClient.Ready && !this.isMigrating && !this.isRecovering && this.isHandlingWork;

    // Disposables
    private SubscriptionClient? subscriptionClient;

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

    // Properties
    protected SubscriptionClient SubscriptionClient { get => this.subscriptionClient ?? throw new InvalidOperationException("SubscriptionClient may not be accessed before CreateAsync is called."); set => this.subscriptionClient = value; }
    
    public ConnectionId ConnectionId { get; }

    // Events
    public event EventHandler<SubscriptionEvent>? OnSubscriptionEvent;

    private void RaiseOnSubscriptionEvent(SubscriptionEvent subscriptionEvent)
    {
        this.OnSubscriptionEvent?.Invoke(this, subscriptionEvent);
    }

    protected SubscriptionConnection(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.ConnectionId = connectionId;
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<SubscriptionConnection>();
    }

    public static async Task<SubscriptionConnection> CreateAsync(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        var connection = new SubscriptionConnection(connectionId, subscriptionClientFactory, loggerFactory);
        await connection.InitializeAsync();
        return connection;
    }

    private async Task InitializeAsync()
    {
        this.SubscriptionClient = await this.CreateNewSubscriptionClientAsync();
        this.isHandlingWork = true;
        this.workerTask = this.HandleSubscriptionWorkAsync();
        this.periodicMigrationTask = this.MigratePeriodically();
    }

    private async Task<SubscriptionClient> CreateNewSubscriptionClientAsync()
    {
        var subscriptionClient = await this.subscriptionClientFactory.CreateAndConnectAsync();
        subscriptionClient.SubscriptionEventReceived += this.HandleSubscriptionEvent;
        subscriptionClient.Disconnected += this.HandleOnWebsocketFaulted;

        return subscriptionClient;
    }

    private async Task CleanupSubscriptionClientAsync(SubscriptionClient subscriptionClient)
    {
        subscriptionClient.SubscriptionEventReceived -= this.HandleSubscriptionEvent;
        subscriptionClient.Disconnected -= this.HandleOnWebsocketFaulted;
        await subscriptionClient.DisposeAsync();
    }

    private void HandleSubscriptionEvent(object? sender, SubscriptionEvent e) => this.RaiseOnSubscriptionEvent(e);

    private void HandleOnWebsocketFaulted(object? sender, EventArgs e) => this.InitiateRecovery();

    private async Task MigratePeriodically()
    {
        while (this.isHandlingWork)
        {
            await Task.Delay(MigrationFrequency);
            this.InitiateMigration();
            await this.migratingTask;
        }
    }

    private void InitiateMigration()
    {
        if (this.disposed || !this.isHandlingWork || this.isMigrating || this.isRecovering)
        {
            return;
        }

        this.logger.LogTrace($"{this.ConnectionId} Initiating migration.");
        this.isMigrating = true;
        this.migratingTask = this.TryMigrateAsync()
                                 .ContinueWith(async t =>
                                 {
                                     if (!t.IsCompletedSuccessfully || t.Result == false)
                                     {
                                         this.InitiateRecovery();
                                     }

                                     this.isMigrating = false;
                                     await this.recoveryTask; // in case we fell into recovery.
                                 });
    }

    private async Task<bool> TryMigrateAsync()
    {
        SubscriptionClient oldClient = this.SubscriptionClient;

        // GET TOKEN
        this.logger.LogTrace("Getting Migration Token.");
        Response getMigrationTokenResponse = await oldClient.GetMigrationTokenAsync(TimeSpan.FromSeconds(15));
        if (!getMigrationTokenResponse.IsCompleted)
        {
            this.logger.LogError($"{this.ConnectionId} Failed to get migration token.");
            return false;
        }
        var contentElement = JsonSerializer.Deserialize<JsonElement>(getMigrationTokenResponse.Message.content);

        if (!contentElement.TryGetProperty("token", out var tokenElement))
        {
            this.logger.LogError($"{this.ConnectionId} Get migration token response message did not contain token element.");
            return false;
        }
        this.logger.LogTrace("Got migration token.");

        // NEW CLIENT
        this.logger.LogTrace("Connecting new client.");
        SubscriptionClient newClient = await this.CreateNewSubscriptionClientAsync();

        // SENT TOKEN
        this.logger.LogTrace("Sending Migration Token.");
        var sendMigrationTokenResponse = await newClient.SendMigrationTokenAsync(tokenElement.GetString()!, MigrationTimeout);
        if (!sendMigrationTokenResponse.IsCompleted)
        {
            this.logger.LogError($"{this.ConnectionId} Send migration token operation did not complete.");
            return false;
        }
        
        // CLEANUP
        this.logger.LogTrace("Finalizing Migration.");
        this.SubscriptionClient = newClient;
        await this.CleanupSubscriptionClientAsync(oldClient);
        return true;
    }

    private void InitiateRecovery()
    {
        this.recoveryTask = Task.Run(this.RecoverConnectionAsync);
    }

    private async Task RecoverConnectionAsync()
    {
        this.isRecovering = true;
        bool isRecovered = false;
        do
        {
            try
            {
                this.logger.LogWarning($"{this.ConnectionId} Recovering connection due to error.");
                // cleanup the old client
                var cleanupTask = this.CleanupSubscriptionClientAsync(this.SubscriptionClient);

                // connect the new one.
                this.SubscriptionClient = await this.CreateNewSubscriptionClientAsync();

                // all subscriptions were dropped, prepare for resubscription.
                this.pendingSubscriptions.Clear();
                this.pendingUnsubscriptions.Clear();

                foreach (var subscription in this.ownedSubscriptions.Keys)
                {
                    this.pendingSubscriptions.Enqueue(subscription);
                }

                await cleanupTask;
                
                isRecovered = true;
                this.logger.LogWarning($"{this.ConnectionId} Recovered with {this.pendingSubscriptions.Count} subscriptions to recover.");
                this.isRecovering = false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"{this.ConnectionId} Failed to recover connection.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        } while (!this.disposed && isRecovered == false);

        this.isRecovering = false;
    }

    public void Subscribe(SubscriptionDefinition[] subscriptionDefinitions)
    {
        // Remove duplicates from the subscription definitions.
        var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

        // Update the subscriptions and the work queue.
        foreach (var subscription in distinctSubscriptions)
        {
            if (this.ownedSubscriptions.TryAdd(subscription, 0))
            {
                this.pendingSubscriptions.Enqueue(subscription);
            }
            else
            {
                this.logger.LogWarning($"{this.ConnectionId} Ignoring duplicate subscription: {subscription}");
            }
        }
    }

    public void Unsubscribe(SubscriptionDefinition[] subscriptionDefinitions)
    {
        // Remove duplicates from the subscription definitions.
        var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

        // Update the queue to include the new items.
        foreach (var subscription in distinctSubscriptions)
        {
            if (this.ownedSubscriptions.TryRemove(subscription, out _))
            {
                this.pendingUnsubscriptions.Enqueue(subscription);
            }
            else
            {
                this.logger.LogWarning($"{this.ConnectionId} Ignoring un-owned subscription: {subscription}");
            }
        }
    }

    private async Task HandleSubscriptionWorkAsync()
    {
        bool idle = false;
        do
        {
            if (this.pendingSubscriptions.IsEmpty || !this.Ready)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            List<SubscriptionDefinition> subscriptionsTaken = new List<SubscriptionDefinition>();
            List<SubscriptionDefinition> unsubscriptionsTaken = new List<SubscriptionDefinition>();

            var subscriptionCount = this.pendingSubscriptions.Count;
            var unsubscriptionCount = this.pendingUnsubscriptions.Count;
            if (subscriptionCount != 0)
            {
                idle = false;
                this.logger.LogInformation($"ConnectionId {this.ConnectionId} has {subscriptionCount} pending subscriptions.  Handling up to {Math.Min(MAX_SUBSCRIPTIONS_AT_ONCE, subscriptionCount)} of them.");

                uint limit = MAX_SUBSCRIPTIONS_AT_ONCE;
                while (limit-- > 0 && !this.pendingSubscriptions.IsEmpty)
                {
                    if (this.pendingSubscriptions.TryDequeue(out var subscription))
                    {
                        subscriptionsTaken.Add(subscription);
                    }
                    else
                    {
                        limit++;
                    }
                }
            }
            else if (unsubscriptionCount != 0)
            {
                idle = false;
                this.logger.LogInformation($"ConnectionId {this.ConnectionId} has {subscriptionCount} pending unsubscriptions.  Handling up to {Math.Min(MAX_SUBSCRIPTIONS_AT_ONCE, subscriptionCount)} of them.");

                uint limit = MAX_SUBSCRIPTIONS_AT_ONCE;
                while (limit-- > 0 && !this.pendingUnsubscriptions.IsEmpty)
                {
                    if (this.pendingUnsubscriptions.TryDequeue(out var subscription))
                    {
                        unsubscriptionsTaken.Add(subscription);
                    }
                    else
                    {
                        limit++;
                    }
                }
            }
            else if (!idle)
            {
                idle = true;
                this.logger.LogInformation($"ConnectionId {this.ConnectionId} has resolved all pending subscriptions and the worker is Idle.");
            }

            Task[] subscriptionTasks = subscriptionsTaken.Select(this.SendSubscriptionRequestAsync).ToArray();

            if (subscriptionTasks.Any())
            {
                Task.WaitAll(subscriptionTasks);
            }

            Task[] unsubscriptionTasks = unsubscriptionsTaken.Select(this.SendUnsubscriptionRequestAsync).ToArray();

            if (unsubscriptionTasks.Any())
            {
                Task.WaitAll(unsubscriptionTasks);
            }
        }
        while (this.isHandlingWork);
    }

    private Task SendSubscriptionRequestAsync(SubscriptionDefinition definition)
    {
        if (!this.ownedSubscriptions.ContainsKey(definition))
        {
            this.logger.LogWarning($"{this.ConnectionId} Ignoring subscription request for unowned subscription: {definition}");
            return Task.CompletedTask;
        }

        // Note, we no inter have the notion of an error response etc.  We will need to handle the message ourselves.
        var response = this.SubscriptionClient.SubscribeAsync(definition.EventId, definition.KeyId, TimeSpan.FromSeconds(15), CancellationToken.None)
            .ContinueWith(task =>
            {
                // handle result.
                if (task.IsFaulted)
                {
                    // handle error.
                    this.logger.LogError($"{this.ConnectionId} Subscription request task faulted.");
                    TryRequeueSubscriptionRequest(definition);
                }

                if (task.IsCanceled)
                {
                    // handle cancel.
                    Debugger.Break();
                }

                if (task.IsCompleted)
                {
                    var result = task.Result;
                    if (result.TimedOut)
                    {
                        // handle failure.
                        this.logger.LogWarning($"{this.ConnectionId} Subscription request timed out.");
                        TryRequeueSubscriptionRequest(definition);
                    }

                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        // handle error.
                        this.logger.LogError($"{this.ConnectionId} Subscription request failed {result.ErrorMessage}");
                        TryRequeueSubscriptionRequest(definition);
                    }

                    if (result.IsCompleted)
                    {
                        // handle success.
                    }
                }

                return task.Result;
            });

        return response;
    }

    private Task SendUnsubscriptionRequestAsync(SubscriptionDefinition definition)
    {
        if (this.ownedSubscriptions.ContainsKey(definition))
        {
            this.logger.LogWarning($"{this.ConnectionId} Ignoring unsubscription request for owned subscription: {definition}");
            return Task.CompletedTask;
        }

        // Note, we no inter have the notion of an error response etc.  We will need to handle the message ourselves.
        var response = this.SubscriptionClient.UnsubscribeAsync(definition.EventId, definition.KeyId, TimeSpan.FromSeconds(15), CancellationToken.None)
            .ContinueWith(task =>
            {
                // handle result.
                if (task.IsFaulted)
                {
                    // handle error.
                    this.logger.LogError($"{this.ConnectionId} Unsubscription request task faulted.");
                }

                if (task.IsCanceled)
                {
                    // handle cancel.
                    Debugger.Break();
                }

                if (task.IsCompleted)
                {
                    var result = task.Result;
                    if (result.TimedOut)
                    {
                        // handle failure.
                        this.logger.LogWarning($"{this.ConnectionId} Unsubscription request timed out.");
                    }

                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        // handle error.
                        this.logger.LogError($"{this.ConnectionId} Unsubscription request failed {result.ErrorMessage}");
                    }

                    if (result.IsCompleted)
                    {
                        // handle success.
                    }
                }

                return task.Result;
            });

        return response;
    }

    private void TryRequeueSubscriptionRequest(SubscriptionDefinition definition)
    {
        do
        {
            if (this.ownedSubscriptions.TryGetValue(definition, out var retryCount))
            {
                if (retryCount < MAX_SUBSCRIPTION_RETRIES)
                {
                    if (this.ownedSubscriptions.TryUpdate(definition, 0, retryCount))
                    {
                        this.pendingSubscriptions.Enqueue(definition);
                        break;
                    }
                }
                else
                {
                    this.logger.LogWarning($"{this.ConnectionId} Subscription request for {definition} failed too many times.  Removing subscription.");
                }
            }
            else
            {
                break;
            }
        } while (true);
    }

    ////////////////////
    // Disposal
    ////////////////////
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            this.disposed = true;
        }
    }

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsyncCore().ConfigureAwait(false);
        this.Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        this.isHandlingWork = false;

        await Task.WhenAll(
            this.workerTask ?? Task.CompletedTask,
            this.periodicMigrationTask ?? Task.CompletedTask);

        if (this.subscriptionClient != null)
        {
            this.subscriptionClient.SubscriptionEventReceived -= this.HandleSubscriptionEvent;
            this.subscriptionClient.Disconnected -= this.HandleOnWebsocketFaulted;
            await this.subscriptionClient.DisposeAsync();
        }
    }
}