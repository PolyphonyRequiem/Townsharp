// See https://aka.ms/new-console-template for more information
using System.Threading.Channels;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
// using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.

//var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET
//var botCreds = new BotCredential("client_idstringfromalta", "ClientSecret-aka the token");
//var webApiClient = new WebApiBotClient(botCreds);

var userCreds = UserCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_USERNAME and TOWNSHARP_PASSWORDHASH
var webApiClient = new WebApiUserClient(userCreds);

var consoleClientFactory = new ConsoleClientFactory();

var joinedServers = (await webApiClient.GetJoinedServersAsync()).ToArray();

//var subscriptionMultiplexerFactory = new SubscriptionMultiplexerFactory(botCreds);
//var subscriptionMultiplexer = subscriptionMultiplexerFactory.Create(10);

await foreach (var server in webApiClient.GetJoinedServersAsyncStream())
{
    if (server.is_online)
    {
        Console.WriteLine($"{server.id} - {server.name}");
    }
}

Console.WriteLine("Enter the server id to connect to:");
string serverIdInput = Console.ReadLine() ?? "";
int serverId = int.Parse(serverIdInput);

var accessRequestResult = await webApiClient.RequestConsoleAccessAsync(serverId);

if (!accessRequestResult.IsSuccess)
{
    throw new InvalidOperationException("Unable to connect to the server.  It is either offline or access was denied.");
}

var accessToken = accessRequestResult.Content.token!;
var endpointUri = accessRequestResult.Content.BuildConsoleUri();

// EVENT HANDLING MODES

// CHANNEL BASED

//Channel<ConsoleEvent> eventChannel = Channel.CreateUnbounded<ConsoleEvent>();
//var messageListenerTask = Task.Run(HandleConsoleEvents);
//var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, eventChannel.Writer);
//async Task HandleConsoleEvents()
//{
//    try
//    {
//        await foreach (var consoleEvent in eventChannel.Reader.ReadAllAsync(cancellationTokenSource.Token))
//        {
//            if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
//            {
//                Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
//            }
//            else
//            {
//                Console.WriteLine(consoleEvent.ToString());
//            }
//        }
//    }
//    catch (Exception)
//    {
//        // no op for now
//    }
//}
// // Connect Client as normal, and await messageListenerTask when the session is ending.

// USER HANDLER ASYNC
// 1). Method group handler
//var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, HandleConsoleEventAsync);
// 2). Lambda handler
//var consoleClient = consoleClientFactory.CreateClient(
//    endpointUri,
//    accessToken,
//    async consoleEvent =>
//    {
//        await Task.Delay(1000); // Let's delay for some reason, just for the async sample.
//        if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
//        {
//            Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
//        }
//        else
//        {
//            Console.WriteLine(consoleEvent.ToString());
//        }
//    });

// USER HANDLER SYNC
// 1). Method group handler
//var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, HandleConsoleEvent);
// 2). Lambda handler
//var consoleClient = consoleClientFactory.CreateClient(
//    endpointUri,
//    accessToken,
//    consoleEvent =>
//    {
//        if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
//        {
//            Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
//        }
//        else
//        {
//            Console.WriteLine(consoleEvent.ToString());
//        }
//    });

var consoleClient = consoleClientFactory.CreateClient(
    endpointUri,
    accessToken,
    consoleEvent =>
    {
        if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
        {
            Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
        }
        else
        {
            Console.WriteLine(consoleEvent.ToString());
        }
    });

// var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, HandleConsoleEvent);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

Console.WriteLine("Connecting to the console.");
await consoleClient.ConnectAsync(cancellationTokenSource.Token); // Connect the client to the console endpoint.
Console.WriteLine("Connected!");

Console.WriteLine("Running command 'player list'");

var result = await consoleClient.RunCommandAsync("player list");

result.HandleResult(
    result => Console.WriteLine($"RESULT:{Environment.NewLine}{result}"),
    error => Console.Error.WriteLine($"ERROR:{Environment.NewLine}{error}"));

cancellationTokenSource.Cancel();

// Asynchronous Handler
//Task HandleConsoleEventAsync(ConsoleEvent consoleEvent)
//{
//    if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
//    {
//        Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
//    }
//    else
//    {
//        Console.WriteLine(consoleEvent.ToString());
//    }

//    return Task.CompletedTask;
//}

// Synchronous Handler
//void HandleConsoleEvent(ConsoleEvent consoleEvent)
//{
//    if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
//    {
//        Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
//    }
//    else
//    {
//        Console.WriteLine(consoleEvent.ToString());
//    }
//}