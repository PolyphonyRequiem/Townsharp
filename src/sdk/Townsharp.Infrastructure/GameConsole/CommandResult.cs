using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.ServerConsole;

public record CommandResult
{
    public static CommandResult Completed(JsonNode result) => new(result, string.Empty, false);

    public static CommandResult Error(string error) => new(null, error, false);

    public static CommandResult Cancelled() => new(null, "Task Cancelled", false);

    public static CommandResult Timeout() => new(null, string.Empty, true);

    private CommandResult(JsonNode? result, string errorMessage, bool timedOut)
    {
        this.Result = result;
        this.ErrorMessage = errorMessage;
        this.TimedOut = timedOut;
    }

    public JsonNode? Result { get; init; }

    public string ErrorMessage { get; init; }

    public bool TimedOut { get; init; }

    public bool IsCompleted => ErrorMessage == string.Empty && !TimedOut;
}