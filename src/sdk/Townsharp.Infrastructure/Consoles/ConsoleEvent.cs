using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.GameConsoles;

public class ConsoleEvent
{
    private readonly JsonNode result;

    public ConsoleEvent(JsonNode result)
    {
        this.result = result;
    }

    public JsonNode Result => this.result;

    public override string ToString() => this.Result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
}