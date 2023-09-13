namespace Townsharp.Servers;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// In designing server, I also design the API for much of Townsharp.  As a result, I expect that this class will change often during early development.
/// 
/// WHAT DO WE NEED?
/// We need to represent the state of the server as it is known to Townsharp, including a server being unknown.
/// We also need to represent the state of the management of a server by Townsharp. (Do we?  Or is that a separate concern?)
/// We probably also need to expose commands, and the ability to access the console for the server, as well as to navigate to the associated group.
/// 
/// The ServerManager could probably accept global event handler callbacks when it is being configured.  Or that could come from the session and be leveraged by the ServerManager.  Really depends on the usage scenario.
/// 
/// NOTES ON DDD:
/// This is not an entity per se, nor is it an aggregate root.  It is a rich domain object all its own.
/// 
/// Console is likely part of the server aggregate here and therefore operations on the console should go through the server.
/// </remarks>
public class Server
{
    private readonly ServerId id;
    private readonly GroupId groupId;
    private readonly ServerState lastKnownState = ServerState.Unknown;

    internal Server(
        ServerId id,
        GroupId groupId)
    {
        this.id = id;
        this.groupId = groupId;
    }

    public ServerId Id => id;

    public GroupId GroupId => groupId;

    public ServerState LastKnownState => this.lastKnownState;

    internal void UpdatePopulation(UserInfo[] newPopulation)
    {
        throw new NotImplementedException();
    }
}

// Minor epiphany.
//session.OnServerAdded += ConfigureNewServer;

//private void ConfigureNewServer(Server newServer)
//{
//    newServer.PopulationChanged += AnnouncePopulationChange;
//    newServer.CommandExecuted += LogExecutedCommandIfNotBot;
//}

// This is actually probably better because it makes the contract more explicit.

// There's a missing piece here though, and it may be "implementing the townsharp bot service" as a matter of inheritance.

//private void ConfigureNewServer(ServerId serverId, GroupId groupId, ServerConfigurationBuilder config)
//{
//    config.ServerEvents.PopulationChangedEvent.RegisterHandler(AnnouncePopulationChanges);
//    config.ServerSettings.ResetToDefault();
//    config.ConsoleCommandQueues.Disable();
//    config.Logging.UseLoggerFactory(this.myBotLoggerFactory);
//}

//private void AnnouncePopulationChanges(Server server, PopulationChangedEvent e)
//{
//    if (e.JoinedPlayers.Any())
//    {
//        Task.Run(() => TryAnnouncePopulationChangeAsync(server, $"{string.Join(", ", e.JoinedPlayers)} entered the world."));
//    }

//    if (e.LeavingPlayers.Any())
//    {
//        Task.Run(() => TryAnnouncePopulationChangeAsync(server, $"{string.Join(", ", e.JoinedPlayers)} left the world."));
//    }
//}

//private Task async TryAnnouncePopulationChangeAsync(Server server, string message)
//{
//    try
//    {
//        var result = await server.RunCommandAsync($"player message * {message} 3");
//        // don't really care if it succeeded or not.
//    }
//    catch (Exception)
//    {
//        // same here.
//    }
//}

//private void LogExecutedCommandIfNotBot(Server server, CommandExecutedEvent e)
//{
//    if (!e.ExecutingUser.IsBot)
//    {
//        this.CommandExecutedLogger.LogWarning($"Command {e.CommandString} was run by user {e.ExecutingUser.ToString()}");
//    }
//}
