namespace Townsharp.Infrastructure.Subscriptions.Models;
public record Response
{
    public static Response Completed(EventMessage eventMessage) => new(eventMessage, string.Empty, false);

    public static Response Error(string error) => new(EventMessage.None, error, false);

    public static Response Cancelled() => new(EventMessage.None, "Task Cancelled", false);

    public static Response Timeout() => new(EventMessage.None, string.Empty, true);

    private Response(EventMessage result, string errorMessage, bool timedOut)
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
