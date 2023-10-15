using System.Text.Json;

namespace Townsharp.Client;

public class ConsoleCommandResult
{
    public bool IsSuccess;

    public JsonElement? Value;
}