// See https://aka.ms/new-console-template for more information
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
var consoleClientFactory = new ConsoleClientFactory();

//var subscriptionMultiplexerFactory = new SubscriptionMultiplexerFactory(botCreds);
//var subscriptionMultiplexer = subscriptionMultiplexerFactory.Create(10);

await foreach (var server in webApiClient.GetJoinedServersAsync())
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


Channel<ConsoleEvent> eventChannel = Channel.CreateUnbounded<ConsoleEvent>(); // not used in this example, but used for handling console events.

var consoleClient = consoleClientFactory.CreateClient(endpointUri, accessToken, eventChannel.Writer);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

var messageListenerTask = Task.Run(HandleConsoleEvents);

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

if (result.IsCompleted)
{
    Console.WriteLine("The command completed.");
    Console.WriteLine(result.Result!);
}
else
{
    Console.Error.WriteLine("Something went wrong.");
    Console.Error.WriteLine(result.ErrorMessage);
}

cancellationTokenSource.Cancel();
await messageListenerTask;

async Task HandleConsoleEvents()
{
    try
    {
        await foreach (var consoleEvent in eventChannel.Reader.ReadAllAsync(cancellationTokenSource.Token))
        {
            if (consoleEvent is PlayerMovedChunkEvent playerMovedChunkEvent)
            {
                Console.WriteLine($"Player {playerMovedChunkEvent.player} moved from chunk {playerMovedChunkEvent.oldChunk} to chunk {playerMovedChunkEvent.newChunk}.");
            }
            else
            {
                Console.WriteLine(consoleEvent.ToString());
            }
        }
    }
    catch (Exception)
    {
        // no op for now
    }
}