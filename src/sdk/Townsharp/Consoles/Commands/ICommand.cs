using System.Text.Json.Nodes;

namespace Townsharp.Consoles.Commands;

public interface ICommand<TResult>
{
    string BuildCommandString();

    TResult FromResponseJson(JsonNode responseJson);

    public static ICommand<string> FromString(string commandString)
    {
        return new UntypedLiteralConsoleCommand(commandString);
    }
}
