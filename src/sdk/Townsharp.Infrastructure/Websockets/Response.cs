namespace Townsharp.Infrastructure.Websockets;

public record Response<TMessage> where TMessage : class
{
    public TMessage? Message { get; init; }

    public string ErrorMessage { get; init; }

    public bool TimedOut { get; init; }

    public bool IsCompleted => ErrorMessage == string.Empty && !TimedOut;

    private Response(TMessage? message, string errorMessage, bool timedOut)
    {
        this.Message = message;
        this.ErrorMessage = errorMessage;
        this.TimedOut = timedOut;
    }

    internal static Response<TMessage> Completed(TMessage message) => new(message, string.Empty, false);

    internal static Response<TMessage> Error(string error) => new(null, error, false);

    internal static Response<TMessage> Cancelled() => new(null, "Task Cancelled", false);

    internal static Response<TMessage> Timeout() => new(null, string.Empty, true);
}
