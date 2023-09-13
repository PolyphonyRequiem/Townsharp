using InventoryTrack.WebApi.Example;

using Townsharp;
using Townsharp.Infrastructure.ServerConsole;
using Townsharp.Infrastructure.WebApi;

internal class InventoryTrackWorker : IHostedService
{
    private readonly ILogger<InventoryTrackWorker> logger;
    private readonly WebApiClient webApiClient;
    private readonly ConsoleClientManager consoleClientManager;
    private readonly InventoryTracker inventoryTracker;
    private Task workerTask = Task.CompletedTask;

    public InventoryTrackWorker(
        WebApiClient webApiClient,
        ConsoleClientManager consoleClientManager,
        InventoryTracker tracker,
        ILogger<InventoryTrackWorker> logger)
    {
        this.webApiClient = webApiClient;
        this.consoleClientManager = consoleClientManager;
        this.inventoryTracker = tracker;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.workerTask = Task.Run(async () =>
        {
            List<Task> pendingTasks = new List<Task>();

            await foreach (var server in this.webApiClient.GetJoinedServersAsync())
            {
                int id = server["id"]?.GetValue<int>() ?? 0;
                int groupId = server["group_id"]?.GetValue<int>() ?? 0;
                this.logger.LogTrace($"Starting inventory tracking for server {id} in group {groupId}.");
                pendingTasks.Add(this.StartServerManagement(new GroupId(groupId), new ServerId(id)));
            }

            await Task.WhenAll(pendingTasks);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private async Task StartServerManagement(GroupId groupId, ServerId serverId)
    {
        await this.consoleClientManager.ManageConsoleForServerAsync(groupId, serverId, OnConnected, OnDisconnected, OnGameConsoleEvent);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void OnConnected(ServerId serverId)
    {
        this.logger.LogTrace($"Server {serverId} connected.");
    }

    private void OnDisconnected(ServerId serverId)
    {
        this.logger.LogTrace($"Server {serverId} disconnected.");
    }

    private void OnGameConsoleEvent(ServerId serverId, GameConsoleEvent gameConsoleEvent)
    {
        var eventType = gameConsoleEvent.Result["eventType"]?.GetValue<string>();
        if (eventType == "InventoryChanged")
        {
            var userId = gameConsoleEvent.Result["data"]?["User"]?["id"]?.GetValue<int>() ?? throw new InvalidDataException("Failed to get user id from event.");
            var userName = gameConsoleEvent.Result["data"]?["User"]?["username"]?.GetValue<string>() ?? throw new InvalidDataException("Failed to get user name from event.");
            var item = gameConsoleEvent.Result["data"]?["ItemName"]?.GetValue<string>() ?? throw new InvalidDataException("Failed to get item name from event.");
            var quantity = gameConsoleEvent.Result["data"]?["Quantity"]?.GetValue<uint>() ?? throw new InvalidDataException("Failed to get quantity from event.");
            var changeType = gameConsoleEvent.Result["data"]?["ChangeType"]?.GetValue<string>() ?? throw new InvalidDataException("Failed to get change type from event.");
            var inventoryType = gameConsoleEvent.Result["data"]?["InventoryType"]?.GetValue<string>() ?? throw new InvalidDataException("Failed to get inventory type from event.");

            var logMessage = $"[Server {serverId.ToString()} | User {userId} ({userName})] - [{changeType}]: {quantity} {item} at {inventoryType}.";
            logger.LogTrace(logMessage);

            logger.LogTrace(gameConsoleEvent.ToString());

            var playerId = new PlayerId(userId);
            var changeEvent = InventoryChangedEvent.Create(changeType, inventoryType, item, quantity);

            this.inventoryTracker.TrackInventoryEvent(serverId, playerId, userName, changeEvent);
        }
        else
        {
            logger.LogWarning(gameConsoleEvent.ToString());
        }
    }
}