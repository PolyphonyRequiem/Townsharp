using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Consoles;

public class CommandResult
{
    public virtual bool IsCompleted { get; }

    public virtual string ErrorMessage { get; }

    public virtual string ResultString { get; }

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

    public static CommandResult Success(string resultString, JsonNode resultJson)
    {
        return new CommandResult(resultString, resultJson);
    }

    public static CommandResult Failure(string errorString)
    {
        return new CommandResult(errorString);
    }

    public void HandleJson(Action<JsonNode> success, Action<string> error)
    {
        if (this.IsCompleted)
        {
            success(this.ResultJson);
        }
        else
        {
            error(this.ErrorMessage);
        }
    }
}

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

    public TResult? Result { get; }

    public static CommandResult<TResult> SuccessResult(TResult result, string resultString, JsonNode resultJson)
    {
        return new CommandResult<TResult>(result, resultString, resultJson);
    }

    public static CommandResult<TResult> FailureResult(string errorString)
    {
        return new CommandResult<TResult>(errorString);
    }

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