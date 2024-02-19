// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
// using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.

var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET
//var botCreds = new BotCredential("client_idstringfromalta", "ClientSecret-aka the token");
var webApiClient = new WebApiBotClient(botCreds);

//var userCreds = UserCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_USERNAME and TOWNSHARP_PASSWORDHASH
//var webApiClient = new WebApiUserClient(userCreds);

var consoleClientFactory = new ConsoleClientFactory();

var joinedServers = (await webApiClient.GetJoinedServersAsync()).ToArray();

//var subscriptionMultiplexerFactory = new SubscriptionMultiplexerFactory(botCreds);
//var subscriptionMultiplexer = subscriptionMultiplexerFactory.Create(10);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.
List<Task> connectAndDumpPlayerListTasks = new List<Task>();

async Task ConnectAndDumpPlayerList(Uri endpointUri, string accessToken)
{
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

    await consoleClient.ConnectAsync(cancellationTokenSource.Token); // Connect the client to the console endpoint.

    var result = await consoleClient.RunCommandAsync("player list");

    // Also subscribe to PlayerMovedChunk event
    await consoleClient.RunCommandAsync("websocket subscribe PlayerMovedChunk"); 

    result.HandleResult(
        result => Console.WriteLine($"RESULT:{Environment.NewLine}{result}"),
        error => Console.Error.WriteLine($"ERROR:{Environment.NewLine}{error}"));

}

ConcurrentBag<ConsoleAccess> consoleAccesses = new();
ConcurrentBag<Task> consoleAccessRequests = new();

await foreach (var server in webApiClient.GetJoinedServersAsyncStream())
{
    if (server.is_online)
    {
        var request = Task.Run(async () =>
        {
            Console.WriteLine($"{server.id} - {server.name}");

            var accessRequestResult = await webApiClient.RequestConsoleAccessAsync(server.id);

            if (!accessRequestResult.IsSuccess)
            {
                throw new InvalidOperationException("Unable to connect to the server.  It is either offline or access was denied.");
            }

            if (!accessRequestResult.Content.IndicatesAccessGranted)
            {
                return;
            }

            consoleAccesses.Add(accessRequestResult.Content);
        });

        consoleAccessRequests.Add(request);
    }
}

await Task.WhenAll(consoleAccessRequests);

Stopwatch sw = new Stopwatch();
sw.Start();

Console.WriteLine($"Total online servers: {consoleAccesses.Count}");

foreach (var consoleAccess in consoleAccesses)
{
    connectAndDumpPlayerListTasks.Add(Task.Run(()=>ConnectAndDumpPlayerList(consoleAccess.BuildConsoleUri(), consoleAccess.token!)));
}

await Task.WhenAll(connectAndDumpPlayerListTasks);
sw.Stop();

Console.WriteLine(sw.ElapsedMilliseconds);

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


// var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, HandleConsoleEvent);

//CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

while (cancellationTokenSource.Token.IsCancellationRequested == false)
{
    await Task.Delay(1000);
}

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

