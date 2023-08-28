namespace Townsharp;

// One Session per Identity Context.  Disambiguate between a user session, a bot session, and "NAYBE" a combined session?
public class Session
{
    protected Session()
    {
        this.servers = new GameServerManager();
    }

    private readonly GameServerManager servers;

    public IReadOnlyDictionary<GameServerId, GameServer> Servers => servers;
}
