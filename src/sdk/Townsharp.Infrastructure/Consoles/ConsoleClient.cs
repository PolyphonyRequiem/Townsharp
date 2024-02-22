using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Consoles;

internal class ConsoleClient : RequestsAndEventsWebsocketClient<ConsoleMessage, CommandResponseMessage, ConsoleSubscriptionEventMessage>, IConsoleClient
{
    // Constants
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(30);
    private readonly Uri consoleEndpoint;
    private readonly string authToken;
    private readonly ChannelWriter<ConsoleEvent> eventChannelWriter;

    private bool isAuthenticated = false;

    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        TypeInfoResolver = ConsoleSerializerContext.Default,
        Converters = { new Vector3Converter() }
    };

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
        return await base.SendRequestAsync(id => JsonSerializer.Serialize(requestMessageFactory(id), jsonSerializerOptions), timeout).ConfigureAwait(false);
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

    Task IConsoleClient.ConnectAsync(CancellationToken cancellationToken)
    {
        return base.ConnectAsync(cancellationToken);
    }

    public Task<CommandResult<string>> RunCommandAsync(string commandString)
    {
        return this.RunCommandAsync(commandString, TimeSpan.FromSeconds(15));
    }

    public async Task<CommandResult<string>> RunCommandAsync(string commandString, TimeSpan timeout)
    {
        var handler = CommandHandler.ForCommand(commandString);

        return await RunCommandWithHandlerAsync(handler, Unit.Value, timeout);
    }

    public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler)
        where TResult : class
    {
        return this.RunCommandWithHandlerAsync(commandHandler, Unit.Value, TimeSpan.FromSeconds(15));
    }

    public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler, TimeSpan timeout)
        where TResult : class
    {
        return this.RunCommandWithHandlerAsync(commandHandler, Unit.Value, timeout);
    }

    public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments)
        where TResult : class
    {
        return this.RunCommandWithHandlerAsync(commandHandler, arguments, TimeSpan.FromSeconds(15));
    }

    public async Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments, TimeSpan timeout)
        where TResult : class
    {
        var commandString = commandHandler.BuildCommandString(arguments);
        var commandResult = await this.RequestAsync(id => new CommandRequestMessage(id, commandString), timeout);

        return commandHandler.GetResultFromCommandResponse(commandResult);
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

    protected override int GetResponseId(CommandResponseMessage responseMessage) => responseMessage.commandId;

    protected override CommandResponseMessage ToResponseMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize<CommandResponseMessage>(document, jsonSerializerOptions) ?? throw new InvalidOperationException("Unable to process the document to message");

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
        var message = JsonSerializer.Deserialize<ConsoleSubscriptionEventMessage>(document, jsonSerializerOptions) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (message == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return ConsoleSubscriptionEventMessage.None;
        }

        return message;
    }

    protected override void HandleEvent(ConsoleSubscriptionEventMessage eventMessage)
    {
        JsonNode data = eventMessage.data ?? new JsonObject();
        ConsoleEvent @event = eventMessage.eventType switch
        {
            "PlayerStateChanged" => JsonSerializer.Deserialize<PlayerStateChangedEvent>(data, jsonSerializerOptions)!,
            "PlayerJoined" => JsonSerializer.Deserialize<PlayerJoinedEvent>(data, jsonSerializerOptions)!,
            "PlayerLeft" => JsonSerializer.Deserialize<PlayerLeftEvent>(data, jsonSerializerOptions)!,
            "PlayerKilled" => JsonSerializer.Deserialize<PlayerKilledEvent>(data, jsonSerializerOptions)!,
            "PopulationModified" => JsonSerializer.Deserialize<PopulationModifiedEvent>(data, jsonSerializerOptions)!,
            "TradeDeckUsed" => JsonSerializer.Deserialize<TradeDeckUsedEvent>(data, jsonSerializerOptions)!,
            "PlayerMovedChunk" => JsonSerializer.Deserialize<PlayerMovedChunkEvent>(data, jsonSerializerOptions)!,
            "ObjectKilled" => JsonSerializer.Deserialize<ObjectKilledEvent>(data, jsonSerializerOptions)!,
            "TrialStarted" => JsonSerializer.Deserialize<TrialStartedEvent>(data, jsonSerializerOptions)!,
            "TrialFinished" => JsonSerializer.Deserialize<TrialFinishedEvent>(data, jsonSerializerOptions)!,
            "InventoryChanged" => JsonSerializer.Deserialize<InventoryChangedEvent>(data, jsonSerializerOptions)!,
            "AtmBalanceChanged" => JsonSerializer.Deserialize<AtmBalanceChangedEvent>(data, jsonSerializerOptions)!,
            "ServerSettingsChanged" => JsonSerializer.Deserialize<ServerSettingsChangedEvent>(data, jsonSerializerOptions)!,
            "CommandExecuted" => JsonSerializer.Deserialize<CommandExecutedEvent>(data, jsonSerializerOptions)!,
            "SocialTabletPlayerBanned" => JsonSerializer.Deserialize<SocialTabletPlayerBannedEvent>(data, jsonSerializerOptions)!,
            "SocialTabletPlayerReported" => JsonSerializer.Deserialize<SocialTabletPlayerReportedEvent>(data, jsonSerializerOptions)!,
            _ => throw new InvalidOperationException($"Unknown event type {eventMessage.eventType}")
        };

        this.eventChannelWriter.TryWrite(@event);
    }

    protected override ConsoleMessage ToMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize<ConsoleMessage>(document, jsonSerializerOptions) ?? throw new InvalidOperationException("Unable to process the document to message");

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
