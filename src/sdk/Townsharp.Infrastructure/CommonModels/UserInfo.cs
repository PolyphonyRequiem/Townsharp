namespace Townsharp.Infrastructure.CommonModels;

public record UserInfo
{
    public int id { get; set; } = -1;

    public string username { get; set; } = string.Empty;

    public static readonly UserInfo None = new ();
}