namespace Townsharp;

public class Session
{
    protected Session()
    {
        this.servers = new GameServerManager();
    }

    private readonly GameServerManager servers;

    public IReadOnlyDictionary<GameServerId, GameServer> Servers => servers;
}
