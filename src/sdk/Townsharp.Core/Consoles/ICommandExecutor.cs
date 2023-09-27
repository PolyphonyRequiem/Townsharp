using System.Text.Json.Nodes;

using FluentResults;

namespace Townsharp.Consoles;

public interface ICommandExecutor
{
    Task<Result<JsonNode>> RunConsoleCommandStringAsync(string consoleCommand);
}