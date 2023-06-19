using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Subscriptions.Models;

public class SubscriptionManager
{
    private readonly SubscriptionMap subscriptionMap;
    private readonly Dictionary<ConnectionId, SubscriptionConnection> connections;
    private readonly ILogger<SubscriptionManager> logger;

    // Events
    private readonly Channel<SubscriptionEvent> eventChannel; 

    protected SubscriptionManager(Dictionary<ConnectionId, SubscriptionConnection> connections, ChannelWriter<SubscriptionEvent> channelWriter, ILogger<SubscriptionManager> logger)
    {
        this.connections = connections;
        this.logger = logger;
        this.subscriptionMap = new SubscriptionMap(this.connections.Keys.ToArray());
        this.eventChannel = Channel.CreateUnbounded<SubscriptionEvent>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = false
        });

        async Task PipeEvents(ChannelReader<SubscriptionEvent> channelReader)
        {
            try
            {
                await foreach (var subscriptionEvent in channelReader.ReadAllAsync())
                {
                    await channelWriter.WriteAsync(subscriptionEvent);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError($"A Subscription Channel has faulted. {ex}");
                channelWriter.TryComplete(ex);
            }
        }

        foreach (var subscriptionConnection in connections.Values)
        {
            var connectionChannel = Channel.CreateUnbounded<SubscriptionEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

            _ = connectionChannel.Reader.Completion.ContinueWith(async (task) =>
            {
                await subscriptionConnection.DisposeAsync();
            });     
            
            _ = Task.Run(() => PipeEvents(connectionChannel.Reader));
        }
    }

    public static async Task<SubscriptionManager> CreateAsync(SubscriptionClientFactory subscriptionClientFactory, ChannelWriter<SubscriptionEvent> channelWriter, ILoggerFactory loggerFactory)
    {
        var connectionIds = Enumerable.Range(0, 10).Select(_ => new ConnectionId());
        var initTasks = connectionIds.Select(id => SubscriptionConnection.CreateAsync(id, subscriptionClientFactory, channelWriter,  loggerFactory));
        var subscriptionConnections = await Task.WhenAll(initTasks)!;
        var subscriptionConnectionsMap = subscriptionConnections.ToDictionary(connection => connection.ConnectionId);
        return new SubscriptionManager(subscriptionConnectionsMap, channelWriter, loggerFactory.CreateLogger<SubscriptionManager>());
    }

    public void RegisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        var newMappings = subscriptionMap.CreateSubscriptionMappingFor(subscriptionDefinitions);

        foreach (var mapping in newMappings)
        {
            this.logger.LogInformation($"Registering {mapping.Value.Length} subscriptions to connection {mapping.Key}.");
            var connection = this.connections[mapping.Key];
            connection.Subscribe(mapping.Value);
        }
    }

    public void UnregisterSubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        throw new NotImplementedException();
        var newMappings = subscriptionMap.CreateUnsubscriptionMappingFor(subscriptionDefinitions);

        foreach (var mapping in newMappings)
        {
            var connection = this.connections[mapping.Key];
            connection.Subscribe(mapping.Value);
        }
    }
}