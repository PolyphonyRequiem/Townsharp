using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.GameConsole;
using Townsharp.Infrastructure.ServerConsole;
using Townsharp.Infrastructure.WebApi;

public class ConsoleRepl : IHostedService
{
    private readonly WebApiClient webApiClient;
    private readonly ConsoleClientFactory consoleSessionFactory;
    private readonly ILogger<ConsoleRepl> logger;

    public ConsoleRepl(WebApiClient webApiClient, ConsoleClientFactory consoleSessionFactory, ILogger<ConsoleRepl> logger)
    {
        this.webApiClient = webApiClient;
        this.consoleSessionFactory = consoleSessionFactory;
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

        var response = await this.webApiClient.RequestConsoleAccessAsync(ulong.Parse(serverId!));

        if (!response["allowed"]?.GetValue<bool>() ?? false)
        {
            throw new InvalidOperationException("Server is not online.");
        }

        UriBuilder uriBuilder = new UriBuilder();

        uriBuilder.Scheme = "ws";
        uriBuilder.Host = response["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
        uriBuilder.Port = response["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

        CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var consoleSession = await this.consoleSessionFactory.CreateAndConnectAsync(
                        uriBuilder.Uri,
                        response["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response."));

        consoleSession.OnGameConsoleEvent += (s, e) => this.logger.LogInformation(e.ToString());
        consoleSession.OnWebsocketFaulted += (s, _) =>
        {
            cancellationTokenSource.Cancel();
            this.logger.LogInformation("Disconnected from server {serverId}.", serverId);
        };
        
        _ = Task.Run(() => this.GetCommands(consoleSession, cancellationTokenSource.Token));
    }

    private async Task GetCommands(ConsoleClient consoleSession, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Console.WriteLine("Enter a command to send to the server:");
            var command = await GetInputAsync(token);

            if (command == "exit" || String.IsNullOrEmpty(command))
            {
                break;
            }

            var result = await consoleSession.RunCommand(command!, TimeSpan.FromSeconds(30), token);

            Console.WriteLine(result.ToString());
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