using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.Subscriptions.Models;
using Townsharp.Infrastructure.Websockets;

namespace Townsharp.Infrastructure.Subscriptions;

internal class SubscriptionClient : RequestsAndEventsWebsocketClient<SubscriptionMessage, SubscriptionResponseMessage, SubscriptionEventMessage>
{
    // Constants
    internal static int MAX_CONCURRENT_REQUESTS = 20;
    private static readonly Uri SubscriptionWebsocketUri = new Uri("wss://websocket.townshiptale.com");

    // Dependencies
    private readonly BotTokenProvider botTokenProvider;
    private readonly ChannelWriter<SubscriptionEvent> eventChannelWriter;

    internal SubscriptionClient(
        BotTokenProvider botTokenProvider,
        ChannelWriter<SubscriptionEvent> eventChannel,
        ILogger<SubscriptionClient> logger)
        : base(logger, MAX_CONCURRENT_REQUESTS)
    {
        this.botTokenProvider = botTokenProvider;
        this.eventChannelWriter = eventChannel;
    }

    // I don't love these return types.
    internal async Task<Response<SubscriptionResponseMessage>> SubscribeAsync(string eventId, int key, TimeSpan timeout)
    {
        return await this.RequestAsync((id, token) => RequestMessage.CreateSubscriptionRequestMessage(id, token, eventId, key), timeout).ConfigureAwait(false);
    }

    internal async Task<Response<SubscriptionResponseMessage>> UnsubscribeAsync(string eventId, int key, TimeSpan timeout)
    {
        return await this.RequestAsync((id, token) => RequestMessage.CreateUnsubscriptionRequestMessage(id, token, eventId, key), timeout).ConfigureAwait(false);
    }

    internal async Task<Response<SubscriptionResponseMessage>> BatchSubscribeAsync(string eventId, int[] keys, TimeSpan timeout)
    {
        return await this.RequestAsync((id, token) => RequestMessage.CreateBatchSubscriptionRequestMessage(id, token, eventId, keys), timeout).ConfigureAwait(false);
    }

    internal async Task<Response<SubscriptionResponseMessage>> GetMigrationTokenAsync(TimeSpan timeout)
    {
        return await this.RequestAsync(RequestMessage.CreateGetMigrationTokenRequestMessage, timeout).ConfigureAwait(false);
    }

    internal async Task<Response<SubscriptionResponseMessage>> SendMigrationTokenAsync(string migrationToken, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return await this.RequestAsync((id, token) => RequestMessage.CreateSendMigrationTokenRequestMessage(id, token, migrationToken), timeout).ConfigureAwait(false);
    }

    private async Task<Response<SubscriptionResponseMessage>> RequestAsync(Func<int, string, RequestMessage> requestMessageFactory, TimeSpan timeout)
    {
        var token = await this.botTokenProvider.GetTokenAsync().ConfigureAwait(false);
        return await base.SendRequestAsync(id => JsonSerializer.Serialize(requestMessageFactory(id, token), SubscriptionsSerializerContext.Default.RequestMessage), timeout).ConfigureAwait(false);
    }

    protected override async Task ConfigureClientWebsocket(ClientWebSocket websocket)
    {
        websocket.Options.SetRequestHeader("Authorization", $"Bearer {await this.botTokenProvider.GetTokenAsync().ConfigureAwait(false)}");
        await websocket.ConnectAsync(SubscriptionWebsocketUri, CancellationToken.None).ConfigureAwait(false);
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


    protected override ErrorInfo CheckResponseForError(SubscriptionResponseMessage response)
    {
        if (response.responseCode >= 400)
        {
            if (response.responseCode >= 400 && response.responseCode < 500)
            {
                return new ErrorInfo(ErrorType.UserError, $"Received {response.responseCode} response code for pending request {response.id} with key {response.key}. Content is '{response.content}'");
            }
            
            return new ErrorInfo(ErrorType.ServiceError, $"Received {response.responseCode} response code for pending request {response.id} with key {response.key}. Content is '{response.content}'");
        }

        return ErrorInfo.None;
    }

    protected override bool IsResponse(SubscriptionMessage message) => message.@event == "response";

    protected override bool IsEvent(SubscriptionMessage message) => message.@event != "response" && !string.IsNullOrEmpty(message.@event);

    protected override SubscriptionMessage ToMessage(JsonDocument document)
    {
        var message = JsonSerializer.Deserialize(document, SubscriptionsSerializerContext.Default.SubscriptionMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (message == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return SubscriptionMessage.None;
        }

        return message;
    }

    protected override SubscriptionResponseMessage ToResponseMessage(JsonDocument document)
    {
        var responseMessage = JsonSerializer.Deserialize(document, SubscriptionsSerializerContext.Default.SubscriptionResponseMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (responseMessage == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return SubscriptionResponseMessage.None;
        }

        return responseMessage;
    }

    protected override SubscriptionEventMessage ToEventMessage(JsonDocument document)
    {
        var eventMessage = JsonSerializer.Deserialize(document, SubscriptionsSerializerContext.Default.SubscriptionEventMessage) ?? throw new InvalidOperationException("Unable to process the document to message");

        if (eventMessage == null)
        {
            this.logger.LogError($"Unable to deserialize message from {document.RootElement.GetRawText()}");
            return SubscriptionEventMessage.None;
        }

        return eventMessage;
    }

    protected override int GetResponseId(SubscriptionResponseMessage responseMessage) => responseMessage.id;

    protected override void HandleEvent(SubscriptionEventMessage eventMessage)
    {
        var subscriptionEvent =  SubscriptionEvent.FromEventMessage(eventMessage);
        this.eventChannelWriter.TryWrite(subscriptionEvent);
    }

    protected override Task OnDisconnectedAsync()
    {
        return base.OnDisconnectedAsync();
    }
}
