using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp;
using Townsharp.Consoles.Commands;

internal class CaveAnnouncerHostedService : IHostedService
{
    private readonly Session session;
    private readonly ILogger<CaveAnnouncerHostedService> logger;

    public CaveAnnouncerHostedService(
        Session session, 
        ILogger<CaveAnnouncerHostedService> logger)
    {
        this.session = session;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(SessionTest);
        return Task.CompletedTask;
    }

    private async Task SessionTest()
    {
        await this.session.initTask;
        // we can likely defer init for managed servers to individual servers.
        
        foreach (var c in this.session.Consoles)
        {
            var id = c.Key;

            try
            {
                var commandResult = await c.Value.RunConsoleCommandAsync(new PlayerListCommand());

                if (commandResult.ConsoleNotAvailable)
                {
                    this.logger.LogWarning($"Attempted to send command to {id} but the console was not available.");
                    continue;
                }

                var playerListString = string.Join(
                        Environment.NewLine,
                        commandResult.Value.Select(
                            p => $"{p.Name} ({p.Id})"));
                this.logger.LogInformation($"{id}:{Environment.NewLine}{playerListString}");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning($"Unable to get players for {id} - {ex.Message}");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}