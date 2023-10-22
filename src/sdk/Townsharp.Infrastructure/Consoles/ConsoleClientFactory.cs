using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Composition;

namespace Townsharp.Infrastructure.Consoles;

public class ConsoleClientFactory
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<ConsoleClientFactory> logger;
    private List<Task> userHandlerTasks = new List<Task>();

    public ConsoleClientFactory()
        : this(InternalLoggerFactory.Default)
    {
    }

    public ConsoleClientFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<ConsoleClientFactory>();
    }

    public ConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, ChannelWriter<ConsoleEvent> eventChannel)
    {
        return new ConsoleClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleClient>());
    }

    public ConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, Func<ConsoleEvent, Task> handleEventAsync)
    {
        var eventChannel = Channel.CreateUnbounded<ConsoleEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

        userHandlerTasks.Add(HandleChannelEventsWithUserProvidedHandler(eventChannel.Reader, handleEventAsync)); // not sure how we will utilize this just yet... Probably in Townsharp.Client?

        return new ConsoleClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleClient>());
    }

    public ConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, Action<ConsoleEvent> handleEvent)
    {
        var eventChannel = Channel.CreateUnbounded<ConsoleEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

        userHandlerTasks.Add(HandleChannelEventsWithUserProvidedHandler(eventChannel.Reader, handleEvent)); // not sure how we will utilize this just yet... Probably in Townsharp.Client?

        return new ConsoleClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleClient>());
    }

    public async Task HandleChannelEventsWithUserProvidedHandler(ChannelReader<ConsoleEvent> channelReader, Func<ConsoleEvent, Task> handleEventAsync)
    {
        try
        {
            await foreach (var consoleEvent in channelReader.ReadAllAsync())
            {
                await handleEventAsync(consoleEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when the channel is closed.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred while handling console events with a user provided handler.");
        }
    }

    public async Task HandleChannelEventsWithUserProvidedHandler(ChannelReader<ConsoleEvent> channelReader, Action<ConsoleEvent> handleEvent)
    {
        try
        {
            await foreach (var consoleEvent in channelReader.ReadAllAsync())
            {
                handleEvent(consoleEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when the channel is closed.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred while handling console events with a user provided handler.");
        }
    }
}
