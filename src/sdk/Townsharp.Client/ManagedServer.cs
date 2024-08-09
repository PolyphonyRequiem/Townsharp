using Townsharp.Infrastructure.Consoles;

namespace Townsharp.Client;

public class ManagedServer
{
    public event Action Online = () => { };
    public event Action Offline = () => { };
    public event Action ConsoleConnected = () => { };

    private Action<PlayerMovedChunkEvent> handlePlayerMovedChunk = _ => { };

    public void HandlePlayerMovedChunk(Action<PlayerMovedChunkEvent> action) => this.handlePlayerMovedChunk = action;

    public Task<ConsoleCommandResult> TryRunCommandAsync(string v)
    {
        return Task.FromResult(new ConsoleCommandResult());
    }
}