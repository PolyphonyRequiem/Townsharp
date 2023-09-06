using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

using Townsharp;
using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.ServerConsole;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

public class ConsoleClientManager
{
    private readonly WebApiClient webApiClient;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly Task<SubscriptionMultiplexer> createSubscriptionManagerTask;
    private readonly ConcurrentDictionary<GameServerId, ManagedConsoleClient> managedConsoleClients = new ConcurrentDictionary<GameServerId, ManagedConsoleClient>();
    private readonly ConcurrentDictionary<ServerGroupId, bool> heartbeatSubscriptions = new ConcurrentDictionary<ServerGroupId, bool>();

    private async Task<SubscriptionMultiplexer> GetSubscriptionManager()
    {
        return await this.createSubscriptionManagerTask;
    }

    public ConsoleClientManager(
        WebApiClient webApiClient, 
        SubscriptionMultiplexerFactory subscriptionManagerFactory, 
        ConsoleClientFactory consoleClientFactory)
    {
        this.webApiClient = webApiClient;
        this.consoleClientFactory = consoleClientFactory;
        this.createSubscriptionManagerTask = InitSubscriptionManagerAsync(subscriptionManagerFactory);
    }

    private async Task<SubscriptionMultiplexer> InitSubscriptionManagerAsync(SubscriptionMultiplexerFactory subscriptionManagerFactory)
    {
        var subscriptionManager = await subscriptionManagerFactory.CreateAsync();

        subscriptionManager.OnSubscriptionEvent += (s, e) =>
        {
            try
            {
                var content = e.Content.Deserialize<JsonObject>()!;
                ulong id = content["id"]?.GetValue<ulong>() ?? 0;

                bool isOnline = content["is_online"]?.GetValue<bool>() ?? false;

                if (isOnline)
                {
                    if (this.managedConsoleClients.ContainsKey(id))
                    {
                        Task.Run(this.managedConsoleClients[id].TryConnectAsync);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        };

        return subscriptionManager;
    }

    public async Task ManageConsoleForServerAsync(
        ServerGroupId serverGroupId,
        GameServerId serverId, 
        Action<GameServerId> onConnected, 
        Action<GameServerId> onDisconnected,
        Action<GameServerId, GameConsoleEvent> onGameConsoleEvent)
    {
        // make sure we have the subscription manager ready.
        var subscriptionManager = await this.GetSubscriptionManager();

        if (this.managedConsoleClients.ContainsKey(serverId))
        {
            throw new InvalidOperationException($"Already managing a console session for server {serverId}");
        }

        if (!this.heartbeatSubscriptions.ContainsKey(serverGroupId))
        {
            subscriptionManager.RegisterSubscriptions(new SubscriptionDefinition[]
            {
                new SubscriptionDefinition("group-server-heartbeat", serverGroupId)
            });

            this.heartbeatSubscriptions.TryAdd(serverGroupId, true);
        }

        var managedConsole = new ManagedConsoleClient(serverId, this.consoleClientFactory, GetServerAccess, onConnected, onDisconnected, onGameConsoleEvent);

        if (this.managedConsoleClients.TryAdd(serverId, managedConsole))
        {
            await managedConsole.TryConnectAsync();
        }
        else
        {
            // nevermind, sorry Console!
        }
    }

    public async Task<ServerAccess> GetServerAccess(GameServerId serverId)
    {
        try
        {
            var response = await this.webApiClient.RequestConsoleAccessAsync(serverId);
            if (!response["allowed"]?.GetValue<bool>() ?? false)
            {
                return ServerAccess.None;
            }

            UriBuilder uriBuilder = new UriBuilder();

            uriBuilder.Scheme = "ws";
            uriBuilder.Host = response["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
            uriBuilder.Port = response["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

            return new ServerAccess(uriBuilder.Uri, response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."));
        }
        catch
        {
            return ServerAccess.None;
        }

        
    }
}