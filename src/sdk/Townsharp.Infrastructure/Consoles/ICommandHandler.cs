using System.Text.Json;
using System.Text.Json.Nodes;

using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Consoles;

/// <summary>
/// Provides methods for creating <see cref="ICommandHandler{TArguments, TResult}"/>s.
/// </summary>
public static class CommandHandler
{
    /// <summary>
    /// Provides an argumentless, string typed command handler for the given command string.
    /// </summary>
    public static ICommandHandler<Unit, string> ForCommand(string command)
    {
        return new UntypedLiteralConsoleCommandHandler(command);
    }
}

/// <summary>
/// Represents a handler for a command on the Game Server's Console endpoint.
/// </summary>
/// <typeparam name="TArguments">The type of the arguments used the command.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
public interface ICommandHandler<TArguments, TResult>
    where TResult : class
{
    string BuildCommandString(TArguments arguments);

    CommandResult<TResult> GetResultFromCommandResponse(Response<CommandResponseMessage> response);
}

internal class UntypedLiteralConsoleCommandHandler : ICommandHandler<Unit, string>
{
    private static JsonSerializerOptions options = new()
    {
        WriteIndented = true
    };

    private string commandString;

    internal UntypedLiteralConsoleCommandHandler(string commandString)
    {
        this.commandString = commandString;
    }

    public string BuildCommandString(Unit _)
    {
        return this.commandString;
    }

    public CommandResult<string> GetResultFromCommandResponse(Response<CommandResponseMessage> response)
    {
        if (response.IsCompleted)
        {
            return CommandResult<string>.SuccessResult(
                response.Message?.data?["Result"]?.ToJsonString() ?? "ERROR",
                response.Message?.data?["ResultString"]?.GetValue<string>() ?? "Error",
                response.Message?.data?["Result"] ?? new JsonObject());
        }
        else
        {
            return CommandResult<string>.FailureResult(response.ErrorMessage);
        }
    }
}

/// <summary>
/// Represents a type with no value, similar to void.  This is used when a command handler does not require any arguments.
public record Unit
{
    /// <summary>
    /// Default and only value of the <see cref="Unit"/> type.
    /// </summary>
    public static readonly Unit Value = new Unit();
}