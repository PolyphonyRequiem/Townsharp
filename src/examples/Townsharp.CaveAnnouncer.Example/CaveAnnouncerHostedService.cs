using System.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp;

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
        // we can likely defer init for managed servers to individual servers.
        await this.session.initTask;
        Stopwatch sw = Stopwatch.StartNew();
        List<Task> tasks = new List<Task>();
        int available = 0;
        int unavailable = 0;
        //foreach (var c in this.session.Consoles)
        //{
        //    var t = Task.Run(async () =>
        //    {
        //        var id = c.Key;

        //        try
        //        {
        //            var commandResult = await c.Value.RunConsoleCommandAsync(new PlayerListCommand());

        //            if (commandResult.ConsoleNotAvailable)
        //            {
        //                this.logger.LogTrace($"Attempted to send command to {id} but the console was not available. {commandResult.ErrorMessage}");
        //                Interlocked.Increment(ref unavailable);
        //                return;
        //            }

        //            var playerListString = string.Join(
        //                    Environment.NewLine,
        //                    commandResult.Value.Select(
        //                        p => $"{p.Name} ({p.Id})"));

        //            this.logger.LogInformation($"{id}:{Environment.NewLine}{playerListString}");

        //            Interlocked.Increment(ref available);
        //        }
        //        catch (Exception ex)
        //        {
        //            this.logger.LogWarning($"Unable to get players for {id} - {ex.Message}");
        //        }
        //    });

        //    tasks.Add(t);
        //}
        await Task.WhenAll(tasks);

        this.logger.LogInformation($"All consoles pinged {sw.Elapsed} - available: {available} - unavailable: {unavailable}");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}