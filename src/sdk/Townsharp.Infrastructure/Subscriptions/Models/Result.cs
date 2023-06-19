namespace Townsharp.Infrastructure.Subscriptions.Models;
public record Result
{
    public static Result Completed(EventMessage eventMessage) => new(eventMessage, string.Empty, false);

    public static Result Error(string error) => new(EventMessage.None, error, false);

    public static Result Cancelled() => new(EventMessage.None, "Task Cancelled", false);

    public static Result Timeout() => new(EventMessage.None, string.Empty, true);

    private Result(EventMessage result, string errorMessage, bool timedOut)
    {
        this.Message = result;
        this.ErrorMessage = errorMessage;
        this.TimedOut = timedOut;
    }

    public EventMessage Message { get; init; }

    public string ErrorMessage { get; init; }

    public bool TimedOut { get; init; }

    public bool IsCompleted => ErrorMessage == string.Empty && !TimedOut;
}
