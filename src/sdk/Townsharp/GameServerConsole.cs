using System.Text.Json.Nodes;

using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.ServerConsole;

namespace Townsharp;

public class GameServerConsole
{
    private readonly GameServerId id;
    private readonly ConsoleSessionFactory consoleSessionFactory;

    internal GameServerConsole(GameServerId id, ConsoleSessionFactory consoleSessionFactory)
    {
        this.id = id;
        this.consoleSessionFactory = consoleSessionFactory;
    }

    private ConsoleState state = ConsoleState.Unknown;

    public ConsoleState State => this.state;

    public async Task<ConsoleCommandResult> RunConsoleCommandAsync(ConsoleCommand command)
    {
        throw new NotImplementedException();
    }
}

public enum ConsoleState
{
    Unknown,
    Connecting,
    Connected,
    Disconnected
}

public record ConsoleCommandResult(string RawResult);

public abstract class ConsoleCommand
{
    protected abstract string Name { get; }

    protected abstract string BuildCommandString();

    protected abstract ConsoleCommandResult BuildResultFromResponse(JsonNode response);

    public static ConsoleCommand FromString(string commandString)
    {
        return new UntypedLiteralConsoleCommand(commandString);
    }
}

internal class UntypedLiteralConsoleCommand : ConsoleCommand
{
    private string commandString;

    public UntypedLiteralConsoleCommand(string commandString)
    {
        this.commandString = commandString;
    }

    protected override string Name => "Unknown Command String";

    protected override string BuildCommandString()
    {
        return this.commandString;
    }

    protected override ConsoleCommandResult BuildResultFromResponse(JsonNode response)
    {
        return new ConsoleCommandResult(response.ToJsonString());
    }
}