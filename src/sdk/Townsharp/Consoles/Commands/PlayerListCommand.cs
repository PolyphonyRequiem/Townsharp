using System.Text.Json;

namespace Townsharp.Consoles.Commands;

public class PlayerListCommand : ICommand<UserInfo[]>
{
    public string BuildCommandString() => "player list";

    public UserInfo[] FromResponseJson(JsonElement responseJson)
    {
        var playerList = responseJson.GetProperty("data").GetProperty("Result").EnumerateArray()
            .Select(p => new UserInfo(p.GetProperty("id").GetInt32(), p.GetProperty("username").GetString() ??"")).ToArray() ?? new UserInfo[0];

        return playerList;
    }    
}
