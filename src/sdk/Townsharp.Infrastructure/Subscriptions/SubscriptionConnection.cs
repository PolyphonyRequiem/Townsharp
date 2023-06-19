using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

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
    // private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(90);
    private static TimeSpan MigrationFrequency = TimeSpan.FromMinutes(5);
    private static TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    // State
    private ImmutableHashSet<SubscriptionDefinition> ownedSubscriptions = ImmutableHashSet<SubscriptionDefinition>.Empty;
    private ConcurrentQueue<SubscriptionDefinition> pendingSubscriptions = new ConcurrentQueue<SubscriptionDefinition>();

    private bool disposed;
    private bool isUnavailable;
    private bool shouldHandleWork;

    // Disposables
    private SubscriptionClient? subscriptionClient;

    // Background Tasks
    private Task? workerTask;
    private Task? migrationTask;

    // Dependencies
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<SubscriptionConnection> logger;

    // Properties
    protected SubscriptionClient SubscriptionClient { get => this.subscriptionClient ?? throw new InvalidOperationException("SubscriptionClient may not be accessed before CreateAsync is called."); set => this.subscriptionClient = value; }
    
    public ConnectionId ConnectionId { get; }

    // Events
    //  this intermediate isn't needed from what I can tell, we could pass the same one from subsciption manager
    private readonly Channel<SubscriptionEvent> eventChannel;

    protected SubscriptionConnection(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ChannelWriter<SubscriptionEvent> channelWriter, ILoggerFactory loggerFactory)
    {
        this.ConnectionId = connectionId;
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<SubscriptionConnection>();

        this.eventChannel = Channel.CreateUnbounded<SubscriptionEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

        _ = this.eventChannel.Reader.ReadAllAsync().ForEachAsync(async (subscriptionEvent) =>
        {
            await channelWriter.WriteAsync(subscriptionEvent);
        });
    }

    public static async Task<SubscriptionConnection> CreateAsync(ConnectionId connectionId, SubscriptionClientFactory subscriptionClientFactory, ChannelWriter<SubscriptionEvent> channelWriter, ILoggerFactory loggerFactory)
    {
        var connection = new SubscriptionConnection(connectionId, subscriptionClientFactory, channelWriter, loggerFactory);
        await connection.InitializeAsync();
        return connection;
    }

    private async Task InitializeAsync()
    {
        this.SubscriptionClient = await CreateSubscriptionClientAsync();
        this.shouldHandleWork = true;
        this.workerTask = this.HandleSubscriptionWorkAsync();
        this.migrationTask = this.MigratePeriodically();
    }

    private async Task<SubscriptionClient> CreateSubscriptionClientAsync()
    {
        var channel = Channel.CreateUnbounded<SubscriptionEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true, 
                SingleReader = true, 
                SingleWriter = true
            });

        var client = await this.subscriptionClientFactory.CreateAsync(channel.Writer);

        _ = channel.Reader.Completion.ContinueWith(async (task) =>
        {
            await client.DisposeAsync();
        });

        async Task PipeEvents()
        {
            try
            {
                await foreach (var subscriptionEvent in channel.Reader.ReadAllAsync())
                {
                    await this.eventChannel.Writer.WriteAsync(subscriptionEvent);
                }
            }
            catch (Exception ex)
            {
                this.isUnavailable = true;
                this.logger.LogWarning($"{ConnectionId} setting as unavailable due to reader fault.");
                this.logger.LogWarning("{connectionId} an error occurred in the SubscriptionClient {ex}", this.ConnectionId, ex);
                _ = Task.Run(RecoverConnectionAsync);
            }
        }

        _ = Task.Run(PipeEvents);

        return client;
    }

    private async Task MigratePeriodically()
    {
        while (this.shouldHandleWork)
        {
            await Task.Delay(MigrationFrequency);
            await this.Migrate();
        }
    }

    private async Task Migrate()
    {
        // "two paths" approach needed
        while (this.isUnavailable)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (this.disposed)
        {
            return;
        }

        this.isUnavailable = true;
        SubscriptionClient oldClient = this.SubscriptionClient;

        this.logger.LogTrace("Getting Migration Token.");

        Result getMigrationTokenResponse = await oldClient.GetMigrationTokenAsync(TimeSpan.FromSeconds(15));

        if (!getMigrationTokenResponse.IsCompleted)
        {
            // Handle failure.
            this.logger.LogError($"{this.ConnectionId} Failed to get migration token.");
            await this.RecoverConnectionAsync();
            return;
        }
        else
        {
            // new client, send migration token
            this.logger.LogTrace("Got migration token.");
            this.logger.LogTrace("Connecting new client.");
            
            SubscriptionClient newClient = await this.CreateSubscriptionClientAsync();

            var contentElement = JsonSerializer.Deserialize<JsonElement>(getMigrationTokenResponse.Message.content);

            if (!contentElement.TryGetProperty("token", out var tokenElement))
            {
                this.logger.LogError($"{this.ConnectionId} Get migration token response message did not contain token element.");
                await this.RecoverConnectionAsync();
                return;
            }

            this.logger.LogTrace("Sending Migration Token.");

            var sendMigrationTokenResponse = await newClient.SendMigrationTokenAsync(tokenElement.GetString()!, MigrationTimeout);

            if (!sendMigrationTokenResponse.IsCompleted)
            {
                this.logger.LogError($"{this.ConnectionId} Send migration token operation did not complete.");
                await this.RecoverConnectionAsync();
                return;
            }
            else
            {
                this.logger.LogTrace("Finalizing Migration.");
                this.SubscriptionClient = newClient;
                await oldClient.DisposeAsync();
            }
        }

        this.isUnavailable = false;
    }

    private async Task RecoverConnectionAsync()
    {
        bool isRecovered = false;
        do
        {
            try
            {
                this.logger.LogWarning($"{this.ConnectionId} Recovering connection due to error.");
                // cleanup the old client
                var disposalTask = this.SubscriptionClient.DisposeAsync();

                // connect the new one.
                this.SubscriptionClient = await CreateSubscriptionClientAsync();
                
                // all subscriptions were dropped, prepare for resubscription.
                this.pendingSubscriptions = new ConcurrentQueue<SubscriptionDefinition>(this.ownedSubscriptions);
                await disposalTask;
                
                isRecovered = true;
                this.logger.LogWarning($"{this.ConnectionId} Recovered with {this.pendingSubscriptions.Count} subscriptions to recover.");
                this.isUnavailable = false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"{this.ConnectionId} Failed to recover connection.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        } while (!this.disposed && !isRecovered);
    }

    public void Subscribe(SubscriptionDefinition[] subscriptionDefinitions)
    {
        // Remove duplicates from the subscription definitions.
        var distinctSubscriptions = subscriptionDefinitions.Distinct().ToArray();

        // Update the subscriptions and the work queue.
        ImmutableInterlocked.Update(ref this.ownedSubscriptions, oldSubscriptions =>
        {
            // Create a new hash set that includes the old subscriptions plus any new ones.
            return oldSubscriptions.Union(distinctSubscriptions);
        });

        // Update the queue to include the new items.
        foreach (var subscription in distinctSubscriptions)
        {
            this.pendingSubscriptions.Enqueue(subscription);
        }
    }

    private async Task HandleSubscriptionWorkAsync()
    {
        const int MaxSubscriptionsAtOnce = 100;
        bool idle = false;
        do
        {
            if (this.pendingSubscriptions.IsEmpty || this.isUnavailable)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            List<SubscriptionDefinition> subscriptionsTaken = new List<SubscriptionDefinition>();
            
            var subscriptionCount = this.pendingSubscriptions.Count;
            if (subscriptionCount != 0)
            {
                idle = false;
                this.logger.LogInformation($"ConnectionId {this.ConnectionId} has {subscriptionCount} pending subscriptions.  Handling up to {Math.Min(MaxSubscriptionsAtOnce, subscriptionCount)} of them.");

                int limit = 100;
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
            else if (!idle)
            {
                idle = true;
                this.logger.LogInformation($"ConnectionId {this.ConnectionId} has resolved all pending subscriptions and the worker is Idle.");
            }

            while (this.isUnavailable)
            {
                // need to actually handle these though.
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Task[] tasks = subscriptionsTaken.Select(this.SendSubscriptionRequestAsync).ToArray();

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
            .ContinueWith(task =>
            {
                // handle result.
                if (task.IsFaulted)
                {
                    // handle error.
                    this.logger.LogError($"{this.ConnectionId} Subscription request task faulted.");
                    this.pendingSubscriptions.Enqueue(definition);
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
                        this.pendingSubscriptions.Enqueue(definition);
                    }

                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        // handle error.
                        this.logger.LogError($"{this.ConnectionId} Subscription request failed {result.ErrorMessage}");
                        this.pendingSubscriptions.Enqueue(definition);
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
        this.shouldHandleWork = false;

        await Task.WhenAll(
            this.workerTask ?? Task.CompletedTask,
            this.migrationTask ?? Task.CompletedTask);

        if (this.subscriptionClient != null)
        {
            this.eventChannel.Writer.TryComplete();
            await this.subscriptionClient.DisposeAsync();
        }
    }
}