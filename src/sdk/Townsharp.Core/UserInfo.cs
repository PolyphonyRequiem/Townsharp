namespace Townsharp;

public class UserInfo
{
    private readonly UserId id;
    private readonly string username;

    public UserInfo(UserId id, string username)
    {
        this.id = id;
        this.username = username;
    }

    public UserId Id => this.id;

    public string Username => this.username;

    public static readonly UserInfo None = new(UserId.None, string.Empty);
}