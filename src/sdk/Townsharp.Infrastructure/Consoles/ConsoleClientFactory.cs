using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Composition;

namespace Townsharp.Infrastructure.Consoles;

public class ConsoleClientFactory
{
    private readonly ILoggerFactory loggerFactory;

    public ConsoleClientFactory()
        : this(InternalLoggerFactory.Default)
    {
    }

    public ConsoleClientFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public ConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, ChannelWriter<ConsoleEvent> eventChannel)
    {
        return new ConsoleClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleClient>());
    }
}
