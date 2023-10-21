using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.WebApi;

public class ConsoleRepl : IHostedService
{
    private readonly WebApiBotClient webApiClient;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly ILogger<ConsoleRepl> logger;

    public ConsoleRepl(WebApiBotClient webApiClient, ConsoleClientFactory consoleClientFactory, ILogger<ConsoleRepl> logger)
    {
        this.webApiClient = webApiClient;
        this.consoleClientFactory = consoleClientFactory;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => StartConsoleReplAsync(cancellationToken));
    }

    private async Task StartConsoleReplAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Enter the server id to connect to:");
        var serverId = Console.ReadLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            bool retryNeeded = false;
            Uri? endpointUri = default;
            string accessToken = "";
            Channel<ConsoleEvent> eventChannel = Channel.CreateUnbounded<ConsoleEvent>();
            try
            {
                var response = await this.webApiClient.RequestConsoleAccessAsync(int.Parse(serverId!));

                if (!response.IsSuccess)
                {
                    logger.LogTrace($"Unable to get access for server {serverId}.  Access was not granted.");
                    throw new InvalidOperationException(); // not good flow control, I'll fix this.  It happened because of changes.
                }

                accessToken = response.Content.token!;
                endpointUri = response.Content.BuildConsoleUri();
            }
            catch (Exception)
            {
                this.logger.LogError($"Unable to get console access for {serverId} at this time.  Will try again in 15s");
                retryNeeded = true;
            }

            if (retryNeeded)
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                continue;
            }

            CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                ConsoleClient consoleClient = this.consoleClientFactory.CreateClient(endpointUri!, accessToken, eventChannel.Writer);
                await consoleClient.ConnectAsync(cancellationTokenSource.Token);
                Task getCommandsTask = this.GetCommandsAsync(consoleClient, cancellationTokenSource.Token); // this doesn't work right, stupid sync console.

                await foreach (var consoleEvent in eventChannel.Reader.ReadAllAsync(cancellationTokenSource.Token))
                {
                    var message = consoleEvent switch
                    {
                        ConsoleEvent e => e.ToString(),
                        _ => "Not Implemented"
                    };

                    Console.WriteLine(message);
                }

                await getCommandsTask;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Something went wrong while trying to run the main console REPL.");
            }
        }
    }

    private async Task GetCommandsAsync(ConsoleClient consoleClient, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Console.WriteLine("Enter a command to send to the server:");
            var command = await GetInputAsync(token);

            if (command == "exit" || String.IsNullOrEmpty(command))
            {
                break;
            }

            var response = await consoleClient.RunCommandAsync(command!);

            if (response.IsCompleted)
            {
                Console.WriteLine(response?.Result);
            }
            else
            {
                Console.WriteLine(response.ToString());
            }
        }
    }

    private Task<string?> GetInputAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(Console.ReadLine, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}