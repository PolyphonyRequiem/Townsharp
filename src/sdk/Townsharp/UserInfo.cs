namespace Townsharp;

public class UserInfo
{
    private readonly ulong id;
    private readonly string name;

    public UserInfo(ulong id, string name)
    {
        this.id = id;
        this.name = name;
    }

    public ulong Id => this.id;

    public string Name => this.name;
}
