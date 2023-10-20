using System.Text.Json;
using System.Text.Json.Nodes;

using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Consoles.Commands;

public class PlayerListCommandHandler : ICommandHandler<Unit, UserInfo[]>
{
    public string BuildCommandString(Unit arguments) => "player list";

    public CommandResult<UserInfo[]> GetResultFromCommandResponse(Response<CommandResponseMessage> response)
    {
        if (response.IsCompleted)
        {
            var playerList = response?.Message?.data?["Result"]?.AsArray()
                .Select(p => new UserInfo(p!["id"]?.GetValue<int>() ?? 0, p!["username"]?.GetValue<string>() ?? "")).ToArray() ?? new UserInfo[0];

            return CommandResult<UserInfo[]>.SuccessResult(playerList, response?.Message?.data?["ResultString"]?.GetValue<string>() ?? "", response?.Message?.data?["Result"] ?? new JsonArray());
        }
        else
        {
            return CommandResult < UserInfo[]>.FailureResult(response.ErrorMessage);
        }
    }
}
