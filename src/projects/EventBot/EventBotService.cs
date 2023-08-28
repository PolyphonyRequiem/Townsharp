using System.Text.Json.Nodes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.ServerConsole;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

public class EventBotService : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ConsoleSessionFactory consoleSessionFactory;
    private readonly ILogger<EventBotService> logger;

    public EventBotService(
        WebApiClient webApiClient, 
        SubscriptionClientFactory subscriptionClientFactory, 
        ConsoleSessionFactory consoleSessionFactory,
        ILogger<EventBotService> logger)
    {
        this.webApiClient = webApiClient;
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.consoleSessionFactory = consoleSessionFactory;
        this.logger = logger;
    }

    ///////////////////////////
    // IHostedService Lifecycle
    ///////////////////////////
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Run(()=> this.RunEventBot(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Not a good plan going forward, but this is where you would do cleanup if any on "graceful" shutdown.
        return Task.CompletedTask;
    }

    ///////////////////////////
    // Service Implementation
    ///////////////////////////
    
    private async Task RunEventBot(CancellationToken cancellationToken)
    {
        // MOST of this complexity will dissolve when using Townsharp directly, for now, we're working off the ugly infra libraries.
        var connectedSubscriptionClient = this.subscriptionClientFactory.CreateAndConnectAsync();

        ulong serverId = 2029794881;  // Silverkeep, but you should replace this.

        JsonObject accessResponseObject = await this.webApiClient.RequestConsoleAccessAsync(serverId);
        ConsoleAccess consoleAccess = ConsoleAccess.GetAccessFromResponseObject(accessResponseObject);

        if (consoleAccess.IsGranted)
        {
            // Let's build our console session.
            CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // I want to change the way this works, but this is it for now.  Subscriptions is a closer example.
            Task consoleLifetimeTask = this.consoleSessionFactory.StartNew(
                consoleAccess.WebsocketUri,
                consoleAccess.AccessToken,
                this.HandleOnConsoleSessionConnected,
                this.HandleSubscribedConsoleEventsReceived,
                this.HandleOnConsoleDisconnected,
                cancellationToken);

            // Let's just take a breather until the console closes.
            await consoleLifetimeTask;
        }
        else
        {
            throw new InvalidOperationException("Sorry, haven't implemented 'Server Offline' handler here yet. We can do that later with global subscriptions");
        }
    }

    private void HandleOnConsoleDisconnected(Exception? exception)
    {
        if (exception == null)
        {
            this.logger.LogInformation("Session closed normally.");
        }
        else
        {
            this.logger.LogError(exception, "Session closed abnormally.");
        }
    }

    private void HandleSubscribedConsoleEventsReceived(ConsoleSession consoleSession, IAsyncEnumerable<GameConsoleEvent> events)
    {
        // This one I especially don't like, but that's okay...
        // We don't handle failures in the lifecycle as written, which could hide error.  Poly needs to fix this.
        _ = Task.Run(async () =>
        {
            await events.ForEachAsync(e =>
            {
                if (e.EventType == "PlayerMovedChunk")
                {
                    this.HandlePlayerMovedChunk(consoleSession, e);
                }
                else
                {
                    logger.LogWarning($"Received an event of type {e.EventType} however, we aren't configured to handle it.");
                }
            });
        });
    }

    private void HandlePlayerMovedChunk(ConsoleSession consoleSession, GameConsoleEvent e)
    {
        this.logger.LogInformation($"EVENT: {e.EventType} - {e}");
    }

    private void HandleOnConsoleSessionConnected(ConsoleSession consoleSession)
    {
        // We should do stuff here, since we just got connected.

        // See, already I can tell I have the wrong paradigm here. It could be as simple as "these should be async" but I doubt it.
        // My console infra isn't meant for direct consumption, so it's not as clean as say, the global subscriptions.

        _ = Task.Run(async () =>
        {
            var response = await consoleSession.RunCommand("websocket subscribe PlayerMovedChunk");

            // probably should check the response here, but let's just move on!
        });
    }

    ///////////////////////////
    // Class Utilities
    ///////////////////////////
    
    // This will be handled in Townsharp directly soon enough.
    private sealed record ConsoleAccess
    {
        public bool IsGranted { get; init; }

        public Uri WebsocketUri { get; init; }

        public string AccessToken { get; init; }

        private ConsoleAccess(Uri websocketUri, string accessToken)
        {
            this.AccessToken = accessToken;
            this.WebsocketUri = websocketUri;
            this.IsGranted = true;
        }

        private ConsoleAccess()
        {
            this.AccessToken = String.Empty;
            this.WebsocketUri = new Uri("ws://localhost:5000");
            this.IsGranted = false;
        }

        private static ConsoleAccess Granted(Uri websocketUri, string accessToken) => new(websocketUri, accessToken);

        private static ConsoleAccess AsDenied() => new ConsoleAccess();

        public static ConsoleAccess GetAccessFromResponseObject(JsonObject responseObject)
        {
            if (!responseObject["allowed"]?.GetValue<bool>() ?? false)
            {
                return AsDenied();
            }

            UriBuilder uriBuilder = new UriBuilder();

            uriBuilder.Scheme = "ws";
            uriBuilder.Host = responseObject["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
            uriBuilder.Port = responseObject["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

            string accessToken = responseObject["token"]?.GetValue<string>() ?? throw new Exception("Failed to get the access token from response");

            return Granted(uriBuilder.Uri, accessToken);
        }
    }
}