using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Consoles;

internal class ConsoleWebsocketClient : RequestsAndEventsWebsocketClient<ConsoleMessage, CommandResponseMessage, ConsoleSubscriptionEventMessage>, IConsoleClient
{
   // Constants
   private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(30);
   private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(120);
   private readonly Uri consoleEndpoint;
   private readonly string authToken;
   private readonly SemaphoreSlim authSemaphore = new SemaphoreSlim(0);
   
   private bool isAuthenticated = false;

   protected override bool IsAuthenticated => this.isAuthenticated;

   private Dictionary<string, bool> subscriptionsRequested = new Dictionary<string, bool>
   {
      { "PlayerStateChanged", false },
      { "PlayerJoined", false },
      { "PlayerLeft", false },
      { "PlayerKilled", false },
      { "PopulationModified", false },
      { "TradeDeckUsed", false },
      { "PlayerMovedChunk", false },
      { "ObjectKilled", false },
      { "TrialStarted", false },
      { "TrialFinished", false },
      { "InventoryChanged", false },
      { "AtmBalanceChanged", false },
      { "ServerSettingsChanged", false },
      { "CommandExecuted", false },
      { "SocialTabletPlayerBanned", false },
      { "SocialTabletPlayerReported", false }
   };

   private event EventHandler<PlayerStateChangedEvent>? playerStateChangedReceivedEvent;
   public event EventHandler<PlayerStateChangedEvent>? PlayerStateChanged
   {
      add
      {
         this.subscriptionsRequested["PlayerStateChanged"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PlayerStateChanged").Wait();
         }
         this.playerStateChangedReceivedEvent += value;
      }
      remove => this.playerStateChangedReceivedEvent -= value;
   }

   private event EventHandler<PlayerJoinedEvent>? playerJoinedReceivedEvent;
   public event EventHandler<PlayerJoinedEvent>? PlayerJoined
   {
      add
      {
         this.subscriptionsRequested["PlayerJoined"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PlayerJoined").Wait();
         }
         this.playerJoinedReceivedEvent += value;
      }
      remove => this.playerJoinedReceivedEvent -= value;
   }

   private event EventHandler<PlayerLeftEvent>? playerLeftReceivedEvent;
   public event EventHandler<PlayerLeftEvent>? PlayerLeft
   {
      add
      {
         this.subscriptionsRequested["PlayerLeft"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PlayerLeft").Wait();
         }
         this.playerLeftReceivedEvent += value;
      }
      remove => this.playerLeftReceivedEvent -= value;
   }

   private event EventHandler<PlayerKilledEvent>? playerKilledReceivedEvent;
   public event EventHandler<PlayerKilledEvent>? PlayerKilledReceivedEvent
   {
      add
      {
         this.subscriptionsRequested["PlayerKilled"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PlayerKilled").Wait();
         }
         this.playerKilledReceivedEvent += value;
      }
      remove => this.playerKilledReceivedEvent -= value;
   }

   private event EventHandler<PopulationModifiedEvent>? populationModifiedReceivedEvent;
   public event EventHandler<PopulationModifiedEvent>? PopulationModified
   {
      add
      {
         this.subscriptionsRequested["PopulationModified"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PopulationModified").Wait();
         }
         this.populationModifiedReceivedEvent += value;
      }
      remove => this.populationModifiedReceivedEvent -= value;
   }

   private event EventHandler<TradeDeckUsedEvent>? tradeDeckUsedReceivedEvent;
   public event EventHandler<TradeDeckUsedEvent>? TradeDeckUsed
   {
      add
      {
         this.subscriptionsRequested["TradeDeckUsed"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("TradeDeckUsed").Wait();
         }
         this.tradeDeckUsedReceivedEvent += value;
      }
      remove => this.tradeDeckUsedReceivedEvent -= value;
   }

   private event EventHandler<PlayerMovedChunkEvent>? playerMovedChunkReceivedEvent;
   public event EventHandler<PlayerMovedChunkEvent>? PlayerMovedChunk
   {
      add
      {
         this.subscriptionsRequested["PlayerMovedChunk"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("PlayerMovedChunk").Wait();
         }
         this.playerMovedChunkReceivedEvent += value;
      }
      remove => this.playerMovedChunkReceivedEvent -= value;
   }

   private event EventHandler<ObjectKilledEvent>? objectKilledReceivedEvent;
   public event EventHandler<ObjectKilledEvent>? ObjectKilledReceivedEvent
   {
      add
      {
         this.subscriptionsRequested["ObjectKilled"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("ObjectKilled").Wait();
         }
         this.objectKilledReceivedEvent += value;
      }
      remove => this.objectKilledReceivedEvent -= value;
   }

   private event EventHandler<TrialStartedEvent>? trialStartedReceivedEvent;
   public event EventHandler<TrialStartedEvent>? TrialStarted
   {
      add
      {
         this.subscriptionsRequested["TrialStarted"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("TrialStarted").Wait();
         }
         this.trialStartedReceivedEvent += value;
      }
      remove => this.trialStartedReceivedEvent -= value;
   }

   private event EventHandler<TrialFinishedEvent>? trialFinishedReceivedEvent;
   public event EventHandler<TrialFinishedEvent>? TrialFinished
   {
      add
      {
         this.subscriptionsRequested["TrialFinished"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("TrialFinished").Wait();
         }
         this.trialFinishedReceivedEvent += value;
      }
      remove => this.trialFinishedReceivedEvent -= value;
   }

   private event EventHandler<InventoryChangedEvent>? inventoryChangedReceivedEvent;
   public event EventHandler<InventoryChangedEvent>? InventoryChanged
   {
      add
      {
         this.subscriptionsRequested["InventoryChanged"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("InventoryChanged").Wait();
         }
         this.inventoryChangedReceivedEvent += value;
      }
      remove => this.inventoryChangedReceivedEvent -= value;
   }

   private event EventHandler<AtmBalanceChangedEvent>? atmBalanceChangedReceivedEvent;
   public event EventHandler<AtmBalanceChangedEvent>? AtmBalanceChanged
   {
      add
      {
         this.subscriptionsRequested["AtmBalanceChanged"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("AtmBalanceChanged").Wait();
         }
         this.atmBalanceChangedReceivedEvent += value;
      }
      remove => this.atmBalanceChangedReceivedEvent -= value;
   }

   private event EventHandler<ServerSettingsChangedEvent>? serverSettingsChangedReceivedEvent;
   public event EventHandler<ServerSettingsChangedEvent>? ServerSettingsChanged
   {
      add
      {
         this.subscriptionsRequested["ServerSettingsChanged"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("ServerSettingsChanged").Wait();
         }
         this.serverSettingsChangedReceivedEvent += value;
      }
      remove => this.serverSettingsChangedReceivedEvent -= value;
   }

   private event EventHandler<CommandExecutedEvent>? commandExecutedReceivedEvent;
   public event EventHandler<CommandExecutedEvent>? CommandExecuted
   {
      add
      {
         this.subscriptionsRequested["CommandExecuted"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("CommandExecuted").Wait();
         }
         this.commandExecutedReceivedEvent += value;
      }
      remove => this.commandExecutedReceivedEvent -= value;
   }

   private event EventHandler<SocialTabletPlayerBannedEvent>? socialTabletPlayerBannedReceivedEvent;
   public event EventHandler<SocialTabletPlayerBannedEvent>? SocialTabletPlayerBanned
   {
      add
      {
         this.subscriptionsRequested["SocialTabletPlayerBanned"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("SocialTabletPlayerBanned").Wait();
         }
         this.socialTabletPlayerBannedReceivedEvent += value;
      }
      remove => this.socialTabletPlayerBannedReceivedEvent -= value;
   }

   private event EventHandler<SocialTabletPlayerReportedEvent>? socialTabletPlayerReportedReceivedEvent;
   public event EventHandler<SocialTabletPlayerReportedEvent>? SocialTabletPlayerReported
   {
      add
      {
         this.subscriptionsRequested["SocialTabletPlayerReported"] = true;
         if (this.isAuthenticated)
         {
            this.SubscribeEventAsync("SocialTabletPlayerReported").Wait();
         }
         this.socialTabletPlayerReportedReceivedEvent += value;
      }
      remove => this.socialTabletPlayerReportedReceivedEvent -= value;
   }

   private static readonly JsonSerializerOptions jsonSerializerOptions = new()
   {
      TypeInfoResolver = ConsoleSerializerContext.Default,
      Converters = { new Vector3Converter() }
   };

   public ConsoleWebsocketClient(Uri consoleEndpoint, string authToken, ILogger<ConsoleWebsocketClient> logger) : base(logger, 1)
   {
      this.consoleEndpoint = consoleEndpoint;
      this.authToken = authToken;
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

   async Task IConsoleClient.ConnectAsync(CancellationToken cancellationToken)
   {
      await base.ConnectAsync(cancellationToken);

      foreach (var subscriptionRequested in this.subscriptionsRequested.Where(s => s.Value == true))
      {
         await this.SubscribeEventAsync(subscriptionRequested.Key);
      }
   }

   public Task<CommandResult<string>> RunCommandAsync(string commandString)
   {
      return this.RunCommandAsync(commandString, DefaultCommandTimeout);
   }

   public async Task<CommandResult<string>> RunCommandAsync(string commandString, TimeSpan timeout)
   {
      var handler = CommandHandler.ForCommand(commandString);

      return await RunCommandWithHandlerAsync(handler, Unit.Value, timeout);
   }

   public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler)
       where TResult : class
   {
      return this.RunCommandWithHandlerAsync(commandHandler, Unit.Value, DefaultCommandTimeout);
   }

   public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TResult>(ICommandHandler<Unit, TResult> commandHandler, TimeSpan timeout)
       where TResult : class
   {
      return this.RunCommandWithHandlerAsync(commandHandler, Unit.Value, timeout);
   }

   public Task<CommandResult<TResult>> RunCommandWithHandlerAsync<TArguments, TResult>(ICommandHandler<TArguments, TResult> commandHandler, TArguments arguments)
       where TResult : class
   {
      return this.RunCommandWithHandlerAsync(commandHandler, arguments, DefaultCommandTimeout);
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

      switch (@event)
      {
         case PlayerStateChangedEvent playerStateChangedEvent:
            this.playerStateChangedReceivedEvent?.Invoke(this, playerStateChangedEvent);
            break;
         case PlayerJoinedEvent playerJoinedEvent:
            this.playerJoinedReceivedEvent?.Invoke(this, playerJoinedEvent);
            break;
         case PlayerLeftEvent playerLeftEvent:
            this.playerLeftReceivedEvent?.Invoke(this, playerLeftEvent);
            break;
         case PlayerKilledEvent playerKilledEvent:
            this.playerKilledReceivedEvent?.Invoke(this, playerKilledEvent);
            break;
         case PopulationModifiedEvent populationModifiedEvent:
            this.populationModifiedReceivedEvent?.Invoke(this, populationModifiedEvent);
            break;
         case TradeDeckUsedEvent tradeDeckUsedEvent:
            this.tradeDeckUsedReceivedEvent?.Invoke(this, tradeDeckUsedEvent);
            break;
         case PlayerMovedChunkEvent playerMovedChunkEvent:
            this.playerMovedChunkReceivedEvent?.Invoke(this, playerMovedChunkEvent);
            break;
         case ObjectKilledEvent objectKilledEvent:
            this.objectKilledReceivedEvent?.Invoke(this, objectKilledEvent);
            break;
         case TrialStartedEvent trialStartedEvent:
            this.trialStartedReceivedEvent?.Invoke(this, trialStartedEvent);
            break;
         case TrialFinishedEvent trialFinishedEvent:
            this.trialFinishedReceivedEvent?.Invoke(this, trialFinishedEvent);
            break;
         case InventoryChangedEvent inventoryChangedEvent:
            this.inventoryChangedReceivedEvent?.Invoke(this, inventoryChangedEvent);
            break;
         case AtmBalanceChangedEvent atmBalanceChangedEvent:
            this.atmBalanceChangedReceivedEvent?.Invoke(this, atmBalanceChangedEvent);
            break;
         case ServerSettingsChangedEvent serverSettingsChangedEvent:
            this.serverSettingsChangedReceivedEvent?.Invoke(this, serverSettingsChangedEvent);
            break;
         case CommandExecutedEvent commandExecutedEvent:
            this.commandExecutedReceivedEvent?.Invoke(this, commandExecutedEvent);
            break;
         case SocialTabletPlayerBannedEvent socialTabletPlayerBannedEvent:
            this.socialTabletPlayerBannedReceivedEvent?.Invoke(this, socialTabletPlayerBannedEvent);
            break;
         case SocialTabletPlayerReportedEvent socialTabletPlayerReportedEvent:
            this.socialTabletPlayerReportedReceivedEvent?.Invoke(this, socialTabletPlayerReportedEvent);
            break;
         default:
            throw new InvalidOperationException($"Unknown event type");
      }
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
   
   private async Task SubscribeEventAsync(string eventName)
   {
      var result = await this.RunCommandAsync($"websocket subscribe {eventName}");

      if (result.IsCompleted && result.Result == "\"Success\"")
      {
         this.logger.LogInformation($"Successfully subscribed to {eventName}");
      }
      else
      {
         this.logger.LogError($"Failed to subscribe to {eventName}");
      }
   }
}
