using System.Text.Json.Nodes;

namespace Townsharp.Consoles.Commands;

public class PlayerListCommand : ICommand<UserInfo[]>
{
    public string BuildCommandString() => "player list";

    public UserInfo[] FromResponseJson(JsonNode responseJson)
    {
        var playerList = responseJson["data"]?["Result"]?.AsArray();

        return playerList?.Select(p => new UserInfo(p?["id"]?.GetValue<int>() ?? 0, p?["username"]?.GetValue<string>()!)).ToArray() ?? new UserInfo[0];
    }    
}
