using System.Text.Json.Nodes;

using Townsharp.Consoles;

namespace Tests.Townsharp.Core.Consoles;
public class ConsoleCommandTests
{
    [Fact]
    public void ConsoleCommand_FromString_CanProduceJsonNodeResult()
    {
        var command = ConsoleCommand.FromString("test");
        Assert.Equal("test", command.GetCommandString());

        var commandWithResult = ConsoleCommand.WithResultFromString("test");
        Assert.Equal("test", commandWithResult.GetCommandString());
        JsonNode responseNode = JsonNode.Parse("{\"test\": \"test\"}")!;
        Assert.Equal(responseNode, commandWithResult.GetResponse(responseNode));
    }
}
