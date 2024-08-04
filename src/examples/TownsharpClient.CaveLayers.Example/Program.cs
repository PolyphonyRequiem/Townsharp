using Microsoft.Extensions.Logging;

using Townsharp.Client;

var botSession = SessionBuilder.Create()
    .AddLogging(c =>
    {
       c.AddConsole();
       c.AddFilter("Townsharp", LogLevel.Trace);
       c.AddFilter("Townsharp.Client.BotSession", LogLevel.Trace);
    })
    .CreateBotSession();

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) => cancellationTokenSource.Cancel();

botSession.HandleGroupAdded(group => Console.WriteLine($"Group added: {group}"));

botSession.HandleServerAdded(
    server =>
    {
        Console.WriteLine($"Server added: {server}");
        server.HandlePlayerMovedChunk(e =>
        {
            if (e.newChunk.StartsWith("Cave Layer"))
            {
                // handle asynchronously without awaiting result.  I will likely support both synchronous and asynchronous versions of this method.
                _ = server.TryRunCommandAsync($"player message {e.player.id} - {e.newChunk} 3");
            }
        });

        server.Online += () => Console.WriteLine($"Server online: {server}");
        server.ConsoleConnected += async () =>
        {
            var commandResult = await server.TryRunCommandAsync("player list");

            if (commandResult.IsSuccess)
            {
                Console.WriteLine($"Players: {commandResult?.Value.ToString() ?? ""}");
            }
        };
    });

await botSession.RunAsync(cancellationTokenSource.Token);