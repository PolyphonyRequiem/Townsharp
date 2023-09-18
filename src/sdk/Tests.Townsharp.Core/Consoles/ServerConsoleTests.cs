using System.Text.Json.Nodes;

using FluentResults;

using NSubstitute;
using Townsharp.Consoles;

namespace Tests.Townsharp.Core.Consoles;

public class ServerConsoleTests
{
    [Fact]
    public async Task ServerConsole_PassesCommandToExecutor()
    {
        var executor = Substitute.For<ICommandExecutor>();
        var console = new ServerConsole(executor);
        console.SetConnected();

        var commandString = "test";
        var command = ConsoleCommand.FromString(commandString);

        await console.RunConsoleCommandAsync(command);

        // Assert that our substitute executor was called with the command string.
        await executor.Received().RunConsoleCommandStringAsync(commandString);
    }

    [Fact]
    public async Task ServerConsole_CanHandleCommandResults()
    {
        var resultJson = JsonNode.Parse("{\"test\": \"test\"}")!;
        var commandString = "test";

        var executor = Substitute.For<ICommandExecutor>();
            executor.RunConsoleCommandStringAsync(commandString).Returns(Result.Ok(resultJson));
        var console = new ServerConsole(executor);
        console.SetConnected();

        var commandWithResult = ConsoleCommand.WithResultFromString(commandString);

        var result = await console.RunConsoleCommandAsync(commandWithResult);

        // Assert that our substitute executor was called with the command string.
        await executor.Received().RunConsoleCommandStringAsync(commandString);

        Assert.Equal(resultJson, result);
    }

    [Fact]
    public async Task ServerConsole_CannotExecuteUnlessConnected()
    {
        var executor = Substitute.For<ICommandExecutor>();
        var console = new ServerConsole(executor);
        
        //console.SetConnected();

        var commandString = "test";
        var command = ConsoleCommand.FromString(commandString);

        // Assert that our substitute executor was called with the command string.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await console.RunConsoleCommandAsync(command));
    }
}

