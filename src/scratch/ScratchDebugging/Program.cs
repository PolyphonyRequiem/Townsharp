// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.WebApi;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.
var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET
var webApiClient = new WebApiBotClient(botCreds);

var consoleClientFactory = new ConsoleClientFactory();

var joinedServers = (await webApiClient.GetJoinedServersAsync()).ToArray();

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

    result.HandleResult(
        result => Console.WriteLine($"RESULT:{Environment.NewLine}{result}"),
        error => Console.Error.WriteLine($"ERROR:{Environment.NewLine}{error}"));

}

ConcurrentBag<ConsoleAccess> consoleAccesses = new ConcurrentBag<ConsoleAccess>();
List<Task> consoleAccessRequests = new();

await foreach (var server in webApiClient.GetJoinedServersAsyncStream())
{
    if (server.is_online && server.id == 1356442845)
    {
        _ = Task.Run(async () =>
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

        var members = await webApiClient.GetGroupMembersAsync(server.group_id);
        foreach (var m in members)
        {
            Console.WriteLine(m.ToString());
        }
    }
}

await Task.WhenAll(connectAndDumpPlayerListTasks);

Stopwatch sw = new Stopwatch();
sw.Start();

foreach (var consoleAccess in consoleAccesses)
{
    connectAndDumpPlayerListTasks.Add(Task.Run(() => ConnectAndDumpPlayerList(consoleAccess.BuildConsoleUri(), consoleAccess.token!)));
}

await Task.WhenAll(connectAndDumpPlayerListTasks);
sw.Stop();

Console.WriteLine(sw.ElapsedMilliseconds);

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};