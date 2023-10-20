using System.Text.Json;
using System.Text.Json.Nodes;

using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Consoles;

public static class CommandHandler
{
    public static ICommandHandler<Unit, string> ForCommand(string command)
    {
        return new UntypedLiteralConsoleCommandHandler(command);
    }
}

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

public record Unit
{
    /// <summary>
    /// Default and only value of the <see cref="Unit"/> type.
    /// </summary>
    public static readonly Unit Value = new Unit();
}