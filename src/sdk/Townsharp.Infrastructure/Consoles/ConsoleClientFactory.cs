using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Consoles.Models;

namespace Townsharp.Infrastructure.GameConsoles;

public class ConsoleClientFactory
{
    private readonly ILoggerFactory loggerFactory;

    public ConsoleClientFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public ConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, ChannelWriter<ConsoleEvent> eventChannel)
    {
        return new ConsoleClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleClient>());
    }
}
