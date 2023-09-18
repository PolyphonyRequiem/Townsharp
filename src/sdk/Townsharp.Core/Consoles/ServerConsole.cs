namespace Townsharp.Consoles;

internal class ServerConsole
{
    private readonly ICommandExecutor commandExecutor;

    private ServerConsoleState state = ServerConsoleState.Unknown;

    public ServerConsole(ICommandExecutor commandExecutor)
    {
        this.commandExecutor = commandExecutor;
    }

    public bool IsConnected => this.state == ServerConsoleState.Connected;

    public async Task RunConsoleCommandAsync(IConsoleCommand consoleCommand)
    {
        if (this.state != ServerConsoleState.Connected)
        {
            throw new InvalidOperationException("Cannot run console command when not connected");
        }

        var result = await commandExecutor.RunConsoleCommandStringAsync(consoleCommand.GetCommandString());

        // if failure, throw exception
    }

    public async Task<TResult> RunConsoleCommandAsync<TResult>(IConsoleCommand<TResult> consoleCommand)
    {
        if (this.state != ServerConsoleState.Connected)
        {
            throw new InvalidOperationException("Cannot run console command when not connected");
        }

        var result = await commandExecutor.RunConsoleCommandStringAsync(consoleCommand.GetCommandString());

        if (!result.IsSuccess)
        {
            // throw exception
            // result.Errors.ForEach(e => Console.WriteLine(e.Message));
        }

        return consoleCommand.GetResponse(result.Value);
    }

    // Events for Events
    //public async Task SubscribeForEvent<TConsoleEvent>

    internal void SetConnected()
    {
        this.state = ServerConsoleState.Connected;

    }

    internal void SetDisconnected()
    {
        this.state = ServerConsoleState.Disconnected;
    }
}
