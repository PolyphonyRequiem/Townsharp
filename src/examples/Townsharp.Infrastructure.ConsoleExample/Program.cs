using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.
var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET

// UNCOMMENT THIS AND LINE 24 BELOW TO ENABLE CONSOLE CLIENT IO LOGGING
//var loggerFactory = LoggerFactory.Create( 
//   builder => 
//   {
//      builder.AddFilter("Townsharp.Infrastructure.Consoles", LogLevel.Trace)
//             .AddConsole();
//   });

var builder = Builders.CreateBotClientBuilder(
   botCreds //, loggerFactory
   );

var subscriptionClient = builder.BuildSubscriptionClient(10);
var webApiClient = builder.BuildWebApiClient();
var joinedServers = (await webApiClient.GetJoinedServersAsync()).ToArray();

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

ConcurrentBag<IConsoleClient> consoles = new ConcurrentBag<IConsoleClient>();

Stopwatch sw = new Stopwatch();
sw.Start();

Console.WriteLine("Checking for online servers.");

foreach (var server in await webApiClient.GetJoinedServersAsync())
{
   if (server.is_online)
   {
      Console.WriteLine($"{server.id} - {server.name}");

      try
      {
         consoles.Add(builder.BuildConsoleClient(webApiClient, server.id));
      }
      catch (Exception ex)
      {
         Console.Error.WriteLine($"EXCEPTION cannot authorize connect to server {server.id}: {ex.Message}");
      }
   }
}

Console.WriteLine($"Total online servers: {consoles.Count}");

foreach (var console in consoles)
{
   _ = Task.Run(async () =>
   {
      console.PlayerMovedChunk += (s, e) => Console.WriteLine($"Player {e.player} moved from chunk {e.oldChunk} to chunk {e.newChunk}.");
      console.PlayerJoined += (s, e) => Console.WriteLine($"Player {e.user} joined the server at position {e.position}");
      console.PlayerLeft += (s, e) => Console.WriteLine($"Player {e.user} left the server.");

      await console.ConnectAsync(cancellationTokenSource.Token); // Connect the client to the console endpoint.

      var result = await console.RunCommandAsync("player list");

      result.HandleResult(
          result => Console.WriteLine($"RESULT:{Environment.NewLine}{result}"),
          error => Console.Error.WriteLine($"ERROR:{Environment.NewLine}{error}"));
   });
}

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
   e.Cancel = true;
   cancellationTokenSource.Cancel();
};

while (cancellationTokenSource.Token.IsCancellationRequested == false)
{
   await Task.Delay(1000);
}