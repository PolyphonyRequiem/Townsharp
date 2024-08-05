namespace Townsharp.Infrastructure.Consoles;

/// <summary>
/// Represents a Game Server Console Client.
/// </summary>
public interface IConsoleClient
{
   event EventHandler<PlayerStateChangedEvent>? PlayerStateChanged;
   event EventHandler<PlayerJoinedEvent>? PlayerJoined;
   event EventHandler<PlayerLeftEvent>? PlayerLeft;
   event EventHandler<PopulationModifiedEvent>? PopulationModified;
   event EventHandler<TradeDeckUsedEvent>? TradeDeckUsed;
   event EventHandler<PlayerMovedChunkEvent>? PlayerMovedChunk;
   event EventHandler<TrialStartedEvent>? TrialStarted;
   event EventHandler<TrialFinishedEvent>? TrialFinished;
   event EventHandler<InventoryChangedEvent>? InventoryChanged;
   event EventHandler<AtmBalanceChangedEvent>? AtmBalanceChanged;
   event EventHandler<ServerSettingsChangedEvent>? ServerSettingsChanged;
   event EventHandler<CommandExecutedEvent>? CommandExecuted;
   event EventHandler<SocialTabletPlayerBannedEvent>? SocialTabletPlayerBanned;
   event EventHandler<SocialTabletPlayerReportedEvent>? SocialTabletPlayerReported;

   /// <summary>
   /// Connects to the Game Server's Console endpoint.
   /// </summary>
   /// <param name="cancellationToken">A cancellation token that can be used to close the connection.</param>
   /// <returns>A task that completes when the connection is fully negotiated and authorized.</returns>
   Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint.
    /// </summary>
    /// <param name="commandString">The command to run.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<string>> RunCommandAsync(string commandString);

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint.
    /// </summary>
    /// <param name="commandString">The command to run.</param>
    /// <param name="timeout">The amount of time to wait for a response from the server before timing out.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<string>> RunCommandAsync(string commandString, TimeSpan timeout);

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint using an <see cref="ICommandHandler{TArguments, TResult}"/>.
    /// </summary>
    /// <typeparam name="TArguments">The type of the arguments to pass to the command handler.</typeparam>
    /// <typeparam name="TResult">The type of the result of the command handler.</typeparam>
    /// <param name="commandHandler">The <see cref="ICommandHandler{TArguments, TResult}"/> to use to run the command.</param>
    /// <param name="arguments">The arguments to pass to the command handler.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments) where TResult : class;

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint using an <see cref="ICommandHandler{TArguments, TResult}"/>.
    /// </summary>
    /// <typeparam name="TArguments">The type of the arguments to pass to the command handler.</typeparam>
    /// <typeparam name="TResult">The type of the result of the command handler.</typeparam>
    /// <param name="commandHandler">The <see cref="ICommandHandler{TArguments, TResult}"/> to use to run the command.</param>
    /// <param name="arguments">The arguments to pass to the command handler.</param>
    /// <param name="timeout">The amount of time to wait for a response from the server before timing out.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments, TimeSpan timeout) where TResult : class;

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint using an <see cref="ICommandHandler{TArguments, TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the command handler.</typeparam>
    /// <param name="commandHandler">The <see cref="ICommandHandler{TArguments, TResult}"/> to use to run the command.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler) where TResult : class;

    /// <summary>
    /// Runs a command on the Game Server's Console endpoint using an <see cref="ICommandHandler{TArguments, TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the command handler.</typeparam>
    /// <param name="commandHandler">The <see cref="ICommandHandler{TArguments, TResult}"/> to use to run the command.</param>
    /// <param name="timeout">The amount of time to wait for a response from the server before timing out.</param>
    /// <returns>A task that completes when the command has been executed and a response has been received.</returns>
    Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler, TimeSpan timeout) where TResult : class;
}