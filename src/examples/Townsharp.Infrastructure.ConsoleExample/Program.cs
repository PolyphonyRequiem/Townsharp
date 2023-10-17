// See https://aka.ms/new-console-template for more information
using System.Threading.Channels;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Consoles.Models;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.

var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET
//var botCreds = new BotCredential("client_idstringfromalta", "ClientSecret-aka the token");

var webApiClient = new WebApiBotClient(botCreds);
var consoleClientFactory = new ConsoleClientFactory();

Console.WriteLine("Enter the server id to connect to:");
string serverIdInput = Console.ReadLine() ?? "";
int serverId = int.Parse(serverIdInput);

// Boilerplate code to get the console access token and connect to the console.
// This will get cleaned up in the future.
var accessRequestResult = await webApiClient.RequestConsoleAccessAsync(serverId);

if (!accessRequestResult["allowed"]?.GetValue<bool>() ?? false)
{
    throw new InvalidOperationException("Server is not online.");
}

var consoleUriBuilder = new UriBuilder()
{
    Scheme = "ws",
    Host = accessRequestResult["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response."),
    Port = accessRequestResult["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response.")
};

string accessToken = accessRequestResult["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response.");

Console.WriteLine("Connecting to the console.");

Channel<ConsoleEvent> eventChannel = Channel.CreateUnbounded<ConsoleEvent>(); // not used in this example, but used for handling console events.

var consoleClient = consoleClientFactory.CreateClient(consoleUriBuilder.Uri, accessToken, eventChannel.Writer);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

await consoleClient.ConnectAsync(cancellationTokenSource.Token); // Connect the client to the console endpoint.

var result = await consoleClient.RunCommandAsync("player list");

if (result.IsCompleted)
{
    Console.WriteLine("The command completed successfully.");
    Console.WriteLine(result.Message);
}
else if (result.TimedOut)
{
    Console.Error.WriteLine("The command took too long.");
    Console.Error.WriteLine(result.ErrorMessage);
}
else
{
    Console.Error.WriteLine("Something went wrong.");
    Console.Error.WriteLine(result.ErrorMessage);
}