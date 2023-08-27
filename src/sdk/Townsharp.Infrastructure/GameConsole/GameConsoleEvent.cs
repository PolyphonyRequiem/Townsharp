using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.ServerConsole;

public class GameConsoleEvent
{
    private readonly JsonNode result;

    public GameConsoleEvent(JsonNode result)
    {
        this.result = result;
    }

    public JsonNode Result => this.result;

    public override string ToString() => Result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented=true});
}