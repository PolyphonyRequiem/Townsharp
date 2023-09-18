using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.GameConsoles;
using Townsharp.Infrastructure.WebApi;

public class ConsoleRepl : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly ILogger<ConsoleRepl> logger;

    public ConsoleRepl(WebApiClient webApiClient, ConsoleClientFactory consoleClientFactory, ILogger<ConsoleRepl> logger)
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
            UriBuilder uriBuilder = new UriBuilder();
            string accessToken = "";
            try
            {
                var response = await this.webApiClient.RequestConsoleAccessAsync(int.Parse(serverId!));

                if (!response["allowed"]?.GetValue<bool>() ?? false)
                {
                    throw new InvalidOperationException("Server is not online.");
                }

                uriBuilder.Scheme = "ws";
                uriBuilder.Host = response["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
                uriBuilder.Port = response["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

                accessToken = response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response.");
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
                ConsoleClient consoleClient = await this.consoleClientFactory.CreateAndConnectAsync(uriBuilder.Uri, accessToken);
                Task getCommandsTask = this.GetCommandsAsync(consoleClient, cancellationTokenSource.Token); // this doesn't work right, stupid sync console.

                void handleGameConsoleEvent(object? sender, ConsoleEvent e)
                {
                    this.logger.LogInformation(e.ToString());
                }

                void handleDisconnected (object? sender,  EventArgs e)
                {
                    cancellationTokenSource.Cancel();
                    this.logger.LogInformation("Disconnected from server {serverId}. Will attempt to reconnect.", serverId);
                    consoleClient.Dispose();
                    consoleClient.GameConsoleEventReceived -= handleGameConsoleEvent;
                    consoleClient.Disconnected -= handleDisconnected;
                }

                consoleClient.GameConsoleEventReceived += handleGameConsoleEvent;
                consoleClient.Disconnected += handleDisconnected;

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

            var result = await consoleClient.RunCommand(command!, TimeSpan.FromSeconds(30), token);

            if (result.IsCompleted)
            {
                Console.WriteLine(result.Result?.Root.ToJsonString(new System.Text.Json.JsonSerializerOptions {WriteIndented=true }) ?? "ERROR PARSING JSON");
            }
            else
            {
                Console.WriteLine(result.ToString());
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