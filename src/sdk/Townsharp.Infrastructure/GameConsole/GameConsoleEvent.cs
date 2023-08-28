using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.ServerConsole;

public class GameConsoleEvent
{
    private readonly JsonNode result;
    private readonly string eventType;

    public GameConsoleEvent(JsonNode result)
    {
        this.result = result;
        this.eventType = result["eventType"]?.GetValue<string>() ?? throw new InvalidOperationException("The console event somehow didn't have an eventType, this shouldn't happen.");
    }

    public JsonNode Result => this.result;

    public string EventType => this.eventType;

    public override string ToString() => Result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented=true});
}