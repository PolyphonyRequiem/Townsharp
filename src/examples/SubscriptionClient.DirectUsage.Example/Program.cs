using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Identity.Models;
using Townsharp.Infrastructure.Subscriptions;

Console.WriteLine("Starting a SubscriptionClient test.");

// Prompt the user for a group to subscribe to heartbeats on.
Console.WriteLine("Please enter a group id to subscribe to heartbeats on:");
var groupIdString = Console.ReadLine();

if (long.TryParse(groupIdString, out var groupId))
{
    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    Task subscribeTask = Subscribe(groupId, cancellationTokenSource.Token);
    
    Console.WriteLine("Press ESC to exit.");

    Task consoleKeyTask = Task.Run(() => 
        {
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();
            do
            {
                keyInfo = Console.ReadKey(true);
            } while (keyInfo.Key != ConsoleKey.Escape); // Wait for an ESC

            cancellationTokenSource.Cancel(); // cancel our subscription task.
        });

    await Task.WhenAll(consoleKeyTask, subscribeTask);
}
else
{
    throw new Exception("Invalid group id, need a numeric value.");
}

async Task Subscribe(long groupId, CancellationToken cancellationToken)
{
    // We need to get a token for our bot to use.  We will use the BotTokenProvider to get one.
    var tokenProvider = new BotTokenProvider(
               new BotCredential(
                   Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID")!,
                   Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET")!),
               new HttpClient());

    // Because the client is disposable asynchronously, we can use the `await using` syntax to ensure proper disposal.
    await using (var client = await SubscriptionClient.CreateAndConnectAsync(tokenProvider, new LoggerFactory().CreateLogger<SubscriptionClient>()))
    {
        // setup our event handler.
        client.OnSubscriptionEvent += (sender, subscriptionEvent) =>
        {
            Console.WriteLine($"Received Event - {subscriptionEvent.EventId}/{subscriptionEvent.KeyId} - {subscriptionEvent.Content.GetRawText()}");
        };

        // They gave us a number, so let's try to subscribe to the heartbeat.
        var result = await client.SubscribeAsync("group-server-heartbeat", groupId, TimeSpan.FromSeconds(15));

        // If the result is completed, then we can check the response code to see if we were successful.
        // Note that this could have produced an error or a timeout instead, but IsCompleted will cover those scenarios for the happy path.
        if (result.IsCompleted)
        {
            if (result.Message.responseCode == 200)
            {
                Console.WriteLine("Successfully subscribed to heartbeat.");
            }
            else
            {
                Console.WriteLine($"Failed to subscribe to heartbeat: Response code was {result.Message.responseCode} - Content is '{result.Message.content}'");
            }
        }
        else
        {
            Console.WriteLine($"Failed to subscribe to heartbeat: {result.ErrorMessage}");
        }

        while (cancellationToken.IsCancellationRequested == false)
        {
            await Task.Delay(TimeSpan.FromSeconds(1)); // let's just await cancellation.
        }
    }
}