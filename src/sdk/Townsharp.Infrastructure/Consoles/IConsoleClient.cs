namespace Townsharp.Infrastructure.Consoles;

public interface IConsoleClient
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<CommandResult<string>> RunCommandAsync(string commandString);
    Task<CommandResult<string>> RunCommandAsync(string commandString, TimeSpan timeout);
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments) where TResult : class;
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments, TimeSpan timeout) where TResult : class;
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler) where TResult : class;
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler, TimeSpan timeout) where TResult : class;
}