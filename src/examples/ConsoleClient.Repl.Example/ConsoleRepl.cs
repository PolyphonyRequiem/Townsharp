using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.WebApi;

public class ConsoleRepl : IHostedService
{
   private readonly BotClientBuilder botClientBuilder;
   private readonly ILogger<ConsoleRepl> logger;
   private readonly WebApiBotClient webApiClient;

   public ConsoleRepl(BotClientBuilder botClientBuilder, ILogger<ConsoleRepl> logger)
   {
      this.botClientBuilder = botClientBuilder;
      this.logger = logger;

      this.webApiClient = this.botClientBuilder.BuildWebApiClient();
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
         CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

         IConsoleClient? consoleClient = null;

         try
         {
            consoleClient = this.botClientBuilder.BuildConsoleClient(this.webApiClient, int.Parse(serverId!));
            await consoleClient.ConnectAsync(cancellationTokenSource.Token);
            consoleClient.ConsoleEvent += HandleConsoleEvent;
            await this.GetCommandsAsync(consoleClient, cancellationTokenSource.Token); // this doesn't work right, stupid sync console.
         }
         catch (Exception ex)
         {
            this.logger.LogError(ex, $"Something went wrong while trying to run the main console REPL.");
         }
         finally
         {
            if (consoleClient != null)
            {
               consoleClient.ConsoleEvent -= HandleConsoleEvent;
            }
         }
      }
   }

   private void HandleConsoleEvent(object? sender, ConsoleEvent e)
   {
      this.logger.LogInformation("EVENT: {eventName} - {event}", Enum.GetName<ConsoleEventType>(e.ConsoleEventType), e);
   }

   private async Task GetCommandsAsync(IConsoleClient consoleClient, CancellationToken token)
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