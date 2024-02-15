using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Consoles;

/// <summary>
/// Represents the result of some command executed on the Game Server's Console endpoint.
/// </summary>
public class CommandResult
{
    /// <summary>
    /// This is true if the command was successfully handled by the server, and a result returned.
    /// Please note that this does not mean that the command was successful, only that the server responded to the command.
    /// </summary>
    public virtual bool IsCompleted { get; }

    /// <summary>
    /// The error message returned by the server, if any.  Should be the empty string if IsCompleted is true.
    /// </summary>
    public virtual string ErrorMessage { get; }

    /// <summary>
    /// The "ResultString" value from the command response.
    /// </summary>
    public virtual string ResultString { get; }

    /// <summary>
    /// The "Result" value from the command response as a JsonNode representing the root of the JSON value.
    /// </summary>
    public virtual JsonNode ResultJson { get; }

    protected CommandResult(string resultString, JsonNode resultJson)
    {
        this.IsCompleted = true;
        this.ResultString = resultString;
        this.ResultJson = resultJson;
        this.ErrorMessage = String.Empty;
    }

    protected CommandResult(string errorMessage)
    {
        this.IsCompleted = true;
        this.ResultString = String.Empty;
        this.ResultJson = new JsonObject();
        this.ErrorMessage = String.Empty;
    }

    /// <summary>
    /// Creates a new <see cref="CommandResult"/> representing a completed command execution.
    /// </summary>
    /// <param name="resultString">The "ResultString" value of the command's response.</param>
    /// <param name="resultJson">The "Result" value from the command response as a JsonNode representing the root of the JSON value.</param>
    /// <returns>The created <see cref="CommandResult"/>.</returns>
    public static CommandResult Completed(string resultString, JsonNode resultJson)
    {
        return new CommandResult(resultString, resultJson);
    }

    /// <summary>
    /// Creates a new <see cref="CommandResult"/> representing a failed command execution.
    /// </summary>
    /// <param name="errorString">The error message returned by the server.</param>
    /// <returns>The created <see cref="CommandResult"/>.</returns>
    public static CommandResult Failure(string errorString)
    {
        return new CommandResult(errorString);
    }

    /// <summary>
    /// A helper method to handle the two possible outcomes of a command.
    /// </summary>
    /// <param name="completed">In the event that the command completed, the provided <see cref="Action"/> will be executed, providing the action with the <see cref="JsonNode"/> of the "Results"</param>
    /// <param name="error">In the event that the command execution encountered an error, the provided <see cref="Action"/> will be executed, providing the action with the error string.</param>
    public void HandleJson(Action<JsonNode> completed, Action<string> error)
    {
        if (this.IsCompleted)
        {
            completed(this.ResultJson);
        }
        else
        {
            error(this.ErrorMessage);
        }
    }
}


/// <summary>
/// Represents the strongly typed result of some command executed on the Game Server's Console endpoint.
/// Used primarily with <see cref="ICommandHandler{TArguments, TResult}"/> to produce strongly typed command results.
/// </summary>
/// <typeparam name="TResult">The Type of the Result of the command in the event of a success.</typeparam>
public class CommandResult<TResult> : CommandResult
{
    protected CommandResult(TResult result, string resultString, JsonNode resultJson)
        : base(resultString, resultJson)
    {
        this.Result = result;
    }

    protected CommandResult(string errorMessage)
        : base(errorMessage)
    {
        this.Result = default!;
    }

    /// <summary>
    /// The strongly typed result of the command as deserialized from the "Result" value of the command response.
    /// This value is <see langword="null"/> if the command failed.
    /// </summary>
    public TResult? Result { get; }

    /// <summary>
    /// Creates a new <see cref="CommandResult{TResult}"/> representing a completed command execution.
    /// </summary>
    /// <param name="result">A strongly typed value representing the "Result" value of the command's response.</param>
    /// <param name="resultString">The "ResultString" value of the command's response.</param>
    /// <param name="resultJson">The "Result" value from the command response as a JsonNode representing the root of the JSON value.</param>
    /// <returns>The created <see cref="CommandResult{TResult}"/>.</returns>
    public static CommandResult<TResult> SuccessResult(TResult result, string resultString, JsonNode resultJson)
    {
        return new CommandResult<TResult>(result, resultString, resultJson);
    }

    /// <summary>
    /// Creates a new <see cref="CommandResult{TResult}"/> representing a failed command execution.
    /// </summary>
    /// <param name="errorString">The error message returned by the server.</param>
    /// <returns>The created <see cref="CommandResult{TResult}"/>.</returns>
    public static CommandResult<TResult> FailureResult(string errorString)
    {
        return new CommandResult<TResult>(errorString);
    }

    /// <summary>
    /// A helper method to handle the two possible outcomes of a command.
    /// </summary>
    /// <param name="success">In the event that the command completed, the provided <see cref="Action"/> will be executed, providing the action with the <see cref="JsonNode"/> of the "Results"</param>
    /// <param name="error">In the event that the command execution encountered an error, the provided <see cref="Action"/> will be executed, providing the action with the error string.</param>
    public void HandleResult(Action<TResult> success, Action<string> error)
    {
        if (this.IsCompleted)
        {
            success(this.Result!);
        }
        else
        {
            error(this.ErrorMessage);
        }
    }
}