namespace Townsharp.Infrastructure.Subscriptions.Models;
public record Response
{
    public static Response Completed(Message eventMessage) => new(eventMessage, string.Empty, false);

    public static Response Error(string error) => new(Message.None, error, false);

    public static Response Cancelled() => new(Message.None, "Task Cancelled", false);

    public static Response Timeout() => new(Message.None, string.Empty, true);

    private Response(Message result, string errorMessage, bool timedOut)
    {
        this.Message = result;
        this.ErrorMessage = errorMessage;
        this.TimedOut = timedOut;
    }

    public Message Message { get; init; }

    public string ErrorMessage { get; init; }

    public bool TimedOut { get; init; }

    public bool IsCompleted => ErrorMessage == string.Empty && !TimedOut;
}
