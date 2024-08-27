// See https://aka.ms/new-console-template for more information
using Townsharp.Infrastructure;
using Townsharp.Infrastructure.Configuration;

Console.WriteLine("Connecting to the bot server.");

// Set up our Townsharp Infrastructure dependencies.
var botCreds = BotCredential.FromEnvironmentVariables(); // reads from TOWNSHARP_CLIENTID and TOWNSHARP_CLIENTSECRET
var builder = Builders.CreateBotClientBuilder(botCreds);
var webApiClient = builder.BuildWebApiClient();

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // used to end the session.

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};