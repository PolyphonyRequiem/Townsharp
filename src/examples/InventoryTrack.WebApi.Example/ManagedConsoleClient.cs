using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.GameConsoles;
using Townsharp.Servers;

public class ManagedConsoleClient
{
    private ServerId serverId;
    private readonly ConsoleClientFactory consoleClientFactory;
    private Func<ServerId, Task<ServerAccess>> getServerAccess;
    private Action<ServerId> onConnected;
    private Action<ServerId> onDisconnected;
    private Action<ServerId, ConsoleEvent> onGameConsoleEvent;

    private bool connected = false;
    private bool connecting = false;

    public ManagedConsoleClient(ServerId serverId, ConsoleClientFactory consoleClientFactory, Func<ServerId, Task<ServerAccess>> getServerAccess, Action<ServerId> onConnected, Action<ServerId> onDisconnected, Action<ServerId, ConsoleEvent> onGameConsoleEvent)
    {
        this.serverId = serverId;
        this.consoleClientFactory = consoleClientFactory;
        this.getServerAccess = getServerAccess;
        this.onConnected = onConnected;
        this.onDisconnected = onDisconnected;
        this.onGameConsoleEvent = onGameConsoleEvent;
    }

    public async Task TryConnectAsync()
    {
        if (this.connected || this.connecting)
        {
            return;
        }

        this.connecting = true;

        try
        {
            var serverAccess = await this.getServerAccess(this.serverId);
            if (serverAccess != ServerAccess.None)
            {
                var consoleClient = await this.consoleClientFactory.CreateAndConnectAsync(serverAccess.Uri, serverAccess.AccessToken);

                consoleClient.Disconnected += (s, _) => this.OnDisconnected(default);
                consoleClient.GameConsoleEventReceived += (s, e) => this.onGameConsoleEvent(this.serverId, e);
                this.OnConnected(consoleClient);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            this.connecting = false;
        }
    }

    private void OnConnected(ConsoleClient console)
    {
        this.connected = true;
        Task.Run(async () =>
        {
            this.onConnected(this.serverId);
            await console.RunCommand("websocket subscribe InventoryChanged", TimeSpan.FromSeconds(15));
            //await console.RunCommand("websocket subscribe CommandExecuted", TimeSpan.FromSeconds(15));
        });
    }
        
    private async void HandleEvents(ConsoleClient session, IAsyncEnumerable<ConsoleEvent> enumerable)
    {
        await foreach (var gameConsoleEvent in enumerable)
        {
            this.onGameConsoleEvent(this.serverId, gameConsoleEvent);
        }
    }

    private void OnDisconnected(Exception? exception)
    {
        this.connected = false;
        this.onDisconnected(this.serverId);
    }
}