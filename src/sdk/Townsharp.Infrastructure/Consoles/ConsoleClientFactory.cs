using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Consoles;

/// <summary>
/// Factory for creating <see cref="IConsoleClient"/> instances.
/// </summary>
public class ConsoleClientFactory
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<ConsoleClientFactory> logger;
    private List<Task> userHandlerTasks = new List<Task>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleClientFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging.</param>
    public ConsoleClientFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<ConsoleClientFactory>();
    }

    /// <summary>
    /// Creates a new <see cref="IConsoleClient"/> instance, using the provided <paramref name="eventChannel"/> to publish events.
    /// </summary>
    /// <param name="consoleWebsocketUri">The URI of the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="BuildConsoleUri"/> method on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="authToken">The authorization token for the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="token"/> property on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="eventChannel">The <see cref="ChannelWriter{T}"/> the <see cref="IConsoleClient"/> should use to publish events asynchronously.</param>
    /// <returns>A new <see cref="IConsoleClient"/> instance.</returns>
    public IConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, ChannelWriter<ConsoleEvent> eventChannel)
    {
        return new ConsoleWebsocketClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleWebsocketClient>());
    }

    /// <summary>
    /// Creates a new <see cref="IConsoleClient"/> instance, using the provided <paramref name="handleEventAsync"/> asynchronous method handler to handle events asynchronously.
    /// </summary>
    /// <param name="consoleWebsocketUri">The URI of the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="BuildConsoleUri"/> method on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="authToken">The authorization token for the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="token"/> property on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="handleEventAsync">The <see cref="Func{T, TResult}"/> to use to handle events asynchronously.</param>
    /// <returns>A new <see cref="IConsoleClient"/> instance.</returns>
    public IConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, Func<ConsoleEvent, Task> handleEventAsync)
    {
        var eventChannel = Channel.CreateUnbounded<ConsoleEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

        userHandlerTasks.Add(HandleChannelEventsWithUserProvidedHandler(eventChannel.Reader, handleEventAsync)); // not sure how we will utilize this just yet... Probably in Townsharp.Client?

        return new ConsoleWebsocketClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleWebsocketClient>());
    }

    /// <summary>
    /// Creates a new <see cref="IConsoleClient"/> instance, using the provided <paramref name="handleEvent"/> handler to handle events.
    /// </summary>
    /// <param name="consoleWebsocketUri">The URI of the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="BuildConsoleUri"/> method on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="authToken">The authorization token for the console websocket as obtained via <see cref="WebApi.WebApiBotClient"/> or <see cref="WebApi.WebApiUserClient"/> calling <see cref="RequestConsoleAccessAsync"/> and using the <see cref="token"/> property on the <see cref="WebApi.ConsoleAccess"/> result type. </param>
    /// <param name="handleEvent">The <see cref="Func{T, TResult}"/> to use to handle events.</param>
    /// <returns>A new <see cref="IConsoleClient"/> instance.</returns>
    public IConsoleClient CreateClient(Uri consoleWebsocketUri, string authToken, Action<ConsoleEvent> handleEvent)
    {
        var eventChannel = Channel.CreateUnbounded<ConsoleEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

        userHandlerTasks.Add(HandleChannelEventsWithUserProvidedHandler(eventChannel.Reader, handleEvent)); // not sure how we will utilize this just yet... Probably in Townsharp.Client?

        return new ConsoleWebsocketClient(consoleWebsocketUri, authToken, eventChannel, this.loggerFactory.CreateLogger<ConsoleWebsocketClient>());
    }

    private async Task HandleChannelEventsWithUserProvidedHandler(ChannelReader<ConsoleEvent> channelReader, Func<ConsoleEvent, Task> handleEventAsync)
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

    private async Task HandleChannelEventsWithUserProvidedHandler(ChannelReader<ConsoleEvent> channelReader, Action<ConsoleEvent> handleEvent)
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
