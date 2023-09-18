namespace Townsharp.Infrastructure.CommonModels;

public record UserInfo
{
    public int Id { get; set; } = -1;

    public string Username { get; set; } = string.Empty;

    public static readonly UserInfo None = new ();
}