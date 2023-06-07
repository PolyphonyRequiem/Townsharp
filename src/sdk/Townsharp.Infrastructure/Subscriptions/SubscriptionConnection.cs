using System.Collections.Immutable;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionConnection : IDisposable, IAsyncDisposable
{
    // Constants
    // private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(90);
    private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(.75);
    private static TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    // State
    private ImmutableHashSet<SubscriptionDefinition> ownedSubscriptions = ImmutableHashSet<SubscriptionDefinition>.Empty;
    private ImmutableQueue<SubscriptionDefinition> pendingSubscriptions = ImmutableQueue<SubscriptionDefinition>.Empty;
    private bool disposed;
    private bool isMigrating;
    private bool shouldHandleWork;

    // Disposables
    private SubscriptionClient? subscriptionClient;

    // Background Tasks
    private Task? workerTask;
    private Task? migrationTask;

    // Dependencies
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILogger<SubscriptionConnection> logger;

    // Properties
    protected SubscriptionClient SubscriptionClient { get => this.subscriptionClient ?? throw new InvalidOperationException("SubscriptionClient may not be accessed before CreateAsync is called."); set => this.subscriptionClient = value; }

    // Events
    public event EventHandler<SubscriptionEvent>? OnSubscriptionEvent;

    private void RaiseOnSubscriptionEvent(SubscriptionEvent subscriptionEvent)
    {
        this.OnSubscriptionEvent?.Invoke(this, subscriptionEvent);
    }

    protected SubscriptionConnection(SubscriptionClientFactory subscriptionClientFactory, ILogger<SubscriptionConnection> logger)
    {
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.logger = logger;
    }

    public static async Task<SubscriptionConnection> CreateAsync(SubscriptionClientFactory subscriptionClientFactory, ILogger<SubscriptionConnection> logger)
    {
        var connection = new SubscriptionConnection(subscriptionClientFactory, logger);
        await connection.InitializeAsync();
        return connection;
    }

    private async Task InitializeAsync()
    {
        this.SubscriptionClient = await this.subscriptionClientFactory.CreateAndConnectAsync();
        this.SubscriptionClient.OnSubscriptionEvent += this.HandleSubscriptionEvent;
        this.shouldHandleWork = true;
        this.workerTask = this.HandleSubscriptionWorkAsync();
        this.migrationTask = this.MigratePeriodically();
    }

    private void HandleSubscriptionEvent(object? sender, SubscriptionEvent e)
    {
        this.RaiseOnSubscriptionEvent(e);
    }

    private async Task MigratePeriodically()
    {
        while (this.shouldHandleWork)
        {
            await Task.Delay(MigrationFrequency);
            await Migrate();
        }
    }

    private async Task Migrate()
    {
        // "two paths" approach needed
        while (this.isMigrating)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (this.disposed)
        {
            return;
        }

        this.isMigrating = true;
        SubscriptionClient oldClient = this.SubscriptionClient;

        this.logger.LogInformation("Getting Migration Token.");

        Response getMigrationTokenResponse = await oldClient.GetMigrationTokenAsync(TimeSpan.FromSeconds(15));

        if (!getMigrationTokenResponse.IsCompleted)
        {
            // Handle failure.
            logger.LogError("Failed to get migration token.");
            await this.RecoverConnectionAsync();
            return;
        }
        else
        {
            // new client, send migration token
            logger.LogInformation("Got migration token.");
            this.logger.LogInformation("Connecting new client.");
            SubscriptionClient newClient = await this.subscriptionClientFactory.CreateAndConnectAsync();

            var contentElement = JsonSerializer.Deserialize<JsonElement>(getMigrationTokenResponse.Message.content);

            if (!contentElement.TryGetProperty("token", out var tokenElement))
            {
                logger.LogError("Get migration token response message did not contain token element.");
                await this.RecoverConnectionAsync();
                return;
            }

            // Setup handle events here.
            newClient.OnSubscriptionEvent += this.HandleSubscriptionEvent;

            this.logger.LogInformation("Sending Migration Token.");

            var sendMigrationTokenResponse = await newClient.SendMigrationTokenAsync(tokenElement.GetString()!, MigrationTimeout);

            if (!sendMigrationTokenResponse.IsCompleted)
            {
                logger.LogError("Send migration token operation did not complete.");
                await this.RecoverConnectionAsync();
                return;
            }
            else
            {
                this.logger.LogInformation("Finalizing Migration.");
                this.SubscriptionClient = newClient;
                await oldClient.DisposeAsync();
                oldClient.OnSubscriptionEvent -= this.HandleSubscriptionEvent;
            }
        }

        this.isMigrating = false;
    }

    private async Task RecoverConnectionAsync()
    {
        bool isRecovered = false;
        do
        {
            try
            {
                this.logger.LogWarning("Recovering connection due to error.");
                this.SubscriptionClient.OnSubscriptionEvent -= this.HandleSubscriptionEvent;
                var disposalTask = this.SubscriptionClient.DisposeAsync();

                this.SubscriptionClient = await this.subscriptionClientFactory.CreateAndConnectAsync();
                this.SubscriptionClient.OnSubscriptionEvent += this.HandleSubscriptionEvent;

                // all subscriptions were dropped, prepare for resubscription.
                ImmutableInterlocked.Update(ref pendingSubscriptions, workQueue =>
                {
                    workQueue.Clear();

                    // Create a new queue that includes all subscriptions.
                    foreach (var subscription in ownedSubscriptions)
                    {
                        workQueue = workQueue.Enqueue(subscription);
                    }
                    return workQueue;
                });
                await disposalTask;
                isRecovered = true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to recover connection.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        } while (!this.disposed && !isRecovered);
    }

    public void Subscribe(SubscriptionDefinition[] subscriptionDefinitions)
    {
        // Remove duplicates from the subscription definitions.
        var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

        // Update the subscriptions and the work queue.
        ImmutableInterlocked.Update(ref ownedSubscriptions, oldSubscriptions =>
        {
            // Create a new hash set that includes the old subscriptions plus any new ones.
            return oldSubscriptions.Union(distinctSubscriptions);
        });

        ImmutableInterlocked.Update(ref pendingSubscriptions, workQueue =>
        {
            // Update the queue to include the new items.
            foreach (var subscription in distinctSubscriptions)
            {
                workQueue = workQueue.Enqueue(subscription);
            }
            return workQueue;
        });
    }

    private async Task HandleSubscriptionWorkAsync()
    {
        do
        {
            if (this.pendingSubscriptions.IsEmpty || this.isMigrating)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            List<SubscriptionDefinition> subscriptionsInWork = new List<SubscriptionDefinition>();

            bool queueUpdated = false;

            while (!this.pendingSubscriptions.IsEmpty && !queueUpdated)
            {
                queueUpdated = ImmutableInterlocked.Update(ref pendingSubscriptions, queue =>
                {
                    subscriptionsInWork.Clear(); // reset in case it takes more than one iteration.
                    int limit = 100;
                    while (limit-- > 0 && !queue.IsEmpty)
                    {
                        queue = queue.Dequeue(out var subscription);
                        subscriptionsInWork.Add(subscription);
                    }

                    return queue;
                });
            }

            while (this.isMigrating)
            {
                // need to actually handle these though.
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Task[] tasks = subscriptionsInWork.Select(s => SendSubscriptionRequestAsync(s)).ToArray();

            if (tasks.Any())
            {
                Task.WaitAll(tasks);
            }
        }
        while (this.shouldHandleWork);
    }

    private Task SendSubscriptionRequestAsync(SubscriptionDefinition definition)
    {
        // Note, we no longer have the notion of an error response etc.  We will need to handle the message ourselves.
        var response = this.SubscriptionClient.SubscribeAsync(definition.EventId, definition.KeyId, TimeSpan.FromSeconds(15), CancellationToken.None)
            .ContinueWith(t =>
            {
                // handle result.
                return t.Result;
            });


        return response;
        // handle success/fail/etc.
    }

    ////////////////////
    // Disposal
    ////////////////////
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        this.shouldHandleWork = false;

        await Task.WhenAll(
            this.workerTask ?? Task.CompletedTask,
            this.migrationTask ?? Task.CompletedTask);

        if (this.subscriptionClient != null)
        {
            this.subscriptionClient.OnSubscriptionEvent -= this.HandleSubscriptionEvent;
            await this.subscriptionClient.DisposeAsync();
        }
    }
}