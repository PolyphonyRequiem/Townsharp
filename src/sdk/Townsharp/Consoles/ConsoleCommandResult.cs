namespace Townsharp.Consoles;

public class ConsoleCommandResult<TResult>
{
    private readonly TResult? result;
    private readonly string errorMessage;
    private readonly bool succeeded;
    private readonly bool consoleNotAvailable;

    public TResult Value =>
        succeeded ?
            this.result ?? throw new InvalidOperationException("Result was not set.") :
            throw new InvalidOperationException($"Unable to get a result from a failed command. {this.errorMessage}");

    public string ErrorMessage =>
        succeeded ?
            throw new InvalidOperationException("The operation succeeded, no error is available.") :
            this.errorMessage;

    public bool ConsoleNotAvailable => consoleNotAvailable;


    private ConsoleCommandResult(string errorMessage, bool consoleNotAvailable)
    {
        this.succeeded = false;
        this.errorMessage = errorMessage;
        this.consoleNotAvailable = consoleNotAvailable;
    }

    public ConsoleCommandResult(TResult result)
    {
        this.succeeded = true;
        this.result = result;
        this.errorMessage = string.Empty;
    }

    internal static ConsoleCommandResult<TResult> AsConsoleNotAvailable()
    {
        return new ConsoleCommandResult<TResult>("No console available.  Try again later.", true);
    }

    internal static ConsoleCommandResult<TResult> AsError(string errorMessage)
    {
        return new ConsoleCommandResult<TResult>(errorMessage, false);
    }
}