using System.Text.Json.Nodes;

namespace Townsharp.Consoles;

public interface IConsoleCommand<TResult> : IConsoleCommand
{
    TResult GetResponse(JsonNode responseJson);
}

public interface IConsoleCommand
{
    string GetCommandString();
}

public static class ConsoleCommand
{
    public static IConsoleCommand FromString(string commandString)
    {
        return new StringConsoleCommand(commandString);
    }

    public static IConsoleCommand<JsonNode> WithResultFromString(string commandString)
    {
        return new StringConsoleCommandWithResult(commandString);
    }

    private class StringConsoleCommand : IConsoleCommand
    {
        private readonly string commandString;

        public StringConsoleCommand(string commandString)
        {
            this.commandString = commandString;
        }

        string IConsoleCommand.GetCommandString()
        {
            return commandString;
        }
    }

    private class StringConsoleCommandWithResult : IConsoleCommand<JsonNode>
    {
        private readonly string commandString;

        public StringConsoleCommandWithResult(string commandString)
        {
            this.commandString = commandString;
        }

        public string GetCommandString()
        {
            return commandString;
        }

        public JsonNode GetResponse(JsonNode responseJson)
        {
            return responseJson;
        }
    }
}