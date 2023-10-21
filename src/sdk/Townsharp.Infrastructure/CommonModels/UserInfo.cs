using System.Text.Json.Serialization;

namespace Townsharp.Infrastructure.CommonModels;

public record UserInfo
{
    [JsonConstructor]
    internal UserInfo(int id, string username)
    {
        this.id = id;
        this.username = username;
    }

    internal UserInfo()
    {
    }

    public int id { get; set; } = -1;

    public string username { get; set; } = string.Empty;

    public static readonly UserInfo None = new();
}