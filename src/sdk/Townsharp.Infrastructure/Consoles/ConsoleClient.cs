using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Consoles.Models;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Consoles;

public class ConsoleClient : RequestsAndEventsWebsocketClient<ConsoleMessage, CommandResponseMessage, ConsoleSubscriptionEventMessage>
{
    // Constants
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(30);
    private readonly Uri consoleEndpoint;
    private readonly string authToken;
    private readonly ChannelWriter<ConsoleEvent> eventChannelWriter;

    private bool isAuthenticated = false;

    protected override bool IsAuthenticated => this.isAuthenticated;
    private readonly SemaphoreSlim authSemaphore = new SemaphoreSlim(0);
    public ConsoleClient(
        Uri consoleEndpoint,
        string authToken,
        ChannelWriter<ConsoleEvent> eventChannel,
        ILogger<ConsoleClient> logger) : base(logger, 1)
    {
        this.consoleEndpoint = consoleEndpoint;
        this.authToken = authToken;
        this.eventChannelWriter = eventChannel;
    }

    private async Task<Response<CommandResponseMessage>> RequestAsync(Func<int, CommandRequestMessage> requestMessageFactory, TimeSpan timeout)
    {
        return await base.SendRequestAsync(id => JsonSerializer.Serialize(requestMessageFactory(id), ConsoleSerializerContext.Default.CommandRequestMessage), timeout).ConfigureAwait(false);
    }

    protected async override Task ConfigureClientWebsocket(ClientWebSocket websocket)
    {
        await websocket.ConnectAsync(this.consoleEndpoint, CancellationToken.None).ConfigureAwait(false);
    }

    protected async override Task<bool> OnConnectedAsync()
    {
        if (!await base.OnConnectedAsync())
        {
            return false;
        }

        // THIS IS NOT HOW WE AUTH!
        await base.SendMessageAsync(this.authToken);
        await authSemaphore.WaitAsync();

        return true;
    }

    protected override void HandleAuthenticationMessage(string message)
    {
        if (message.Contains("Connection Succeeded, Authenticated as:"))
        {
            this.isAuthenticated = true;
        }

        this.authSemaphore.Release();
    }

    public Task<Response<CommandResponseMessage>> RunCommandAsync(string commandString)
    {
        return this.RequestAsync(id => new CommandRequestMessage(id, commandString), TimeSpan.FromSeconds(15));
    }

    public async Task<Response<CommandResponseMessage>> RunCommand(string commandString, TimeSpan timeout)
    {
        return await this.RequestAsync(id => new CommandRequestMessage(id, commandString), timeout);
    }

    protected override ErrorInfo CheckForError(string message) => new ErrorInfo(ErrorType.FatalError, message);

    protected override ErrorInfo CheckForError(JsonDocument document)
    {
        if (document.RootElement.TryGetProperty("connectionId", out _))
        {
            return new ErrorInfo(ErrorType.InfrastructureError, $"An infrastructure error has occurred. {document.RootElement.GetRawText()}");
        }

        return ErrorInfo.None;
    }

    protected override bool IsResponse(ConsoleMessage message)
    {
        return message.type == "CommandResult" || message.type == "SystemMessage";
    }

    protected override ErrorInfo CheckResponseForError(CommandResponseMessage response)
    {
        return ErrorInfo.None; // data.Result == Success
    }

    protected override int GetResponseId(CommandResponseMessage responseMessage) =>  responseMessage.commandId;

    protected override CommandResponseMessage ToResponseMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize(document, ConsoleSerializerContext.Default.CommandResponseMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (message == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return CommandResponseMessage.None;
        }

        return message;
    }

    protected override bool IsEvent(ConsoleMessage message)
    {
        return message.type == "Subscription";
    }

    protected override ConsoleSubscriptionEventMessage ToEventMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize(document, ConsoleSerializerContext.Default.ConsoleSubscriptionEventMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (message == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return ConsoleSubscriptionEventMessage.None;
        }

        return message;
    }

    protected override void HandleEvent(ConsoleSubscriptionEventMessage eventMessage)
    {
        JsonElement data = eventMessage.data.GetValueOrDefault();
        ConsoleEvent @event = eventMessage.eventType switch
        {
            "PlayerMovedChunk" => JsonSerializer.Deserialize(data, ConsoleSerializerContext.Default.PlayerMovedChunkEvent)!,
            "PlayerJoined" => JsonSerializer.Deserialize(data, ConsoleSerializerContext.Default.PlayerJoinedEvent)!,
            "PlayerLeft" => JsonSerializer.Deserialize(data, ConsoleSerializerContext.Default.PlayerLeftEvent)!,
            _ => throw new InvalidOperationException($"Unknown event type {eventMessage.eventType}")
        };

        this.eventChannelWriter.TryWrite(@event);
    }

    protected override ConsoleMessage ToMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize(document, ConsoleSerializerContext.Default.ConsoleMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (message == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return ConsoleMessage.None;
        }

        return message;
    }

    protected override Task OnDisconnectedAsync()
    {
        return base.OnDisconnectedAsync();
    }
}
