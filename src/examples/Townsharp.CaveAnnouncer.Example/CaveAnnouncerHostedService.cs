using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp;
using Townsharp.Consoles;
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
        this.session.TEST_AddGameServerConsole(1685353492);
        var server = this.session.Consoles[1685353492];
        var commandResult = await server.RunConsoleCommandAsync(new PlayerListCommand());
        this.logger.LogInformation(
            string.Join(
                Environment.NewLine,
                commandResult.Value.Select(
                    p => $"{p.Name} ({p.Id})")));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}