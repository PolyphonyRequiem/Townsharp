using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using FluentResults;

using Microsoft.Extensions.Logging;

using Townsharp.Consoles.Commands;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Consoles.Models;
using Townsharp.Infrastructure.GameConsoles;
using Townsharp.Internals.Consoles;
using Townsharp.Servers;

namespace Townsharp.Consoles;

public class GameServerConsole
{
    private readonly ServerId id;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly ConsoleAccessProvider consoleAccessProvider;
    private readonly ILogger<GameServerConsole> logger;
    private Task<ConsoleClient?> consoleClientFactoryTask = Task.FromResult<ConsoleClient?>(null);

    private readonly SemaphoreSlim semaphore = new(1, 1);

    internal GameServerConsole(
        ServerId id,
        ConsoleClientFactory consoleClientFactory,
        ConsoleAccessProvider consoleAccessProvider,
        ILogger<GameServerConsole> logger)
    {
        this.id = id;
        this.consoleClientFactory = consoleClientFactory;
        this.consoleAccessProvider = consoleAccessProvider;
        this.logger = logger;
    }

    private ConsoleState consoleState = ConsoleState.Disconnected;

    internal void TryToConnect()
    {
        if (consoleState == ConsoleState.Connected)
        {
            logger.LogTrace($"Attempted to reconnect {nameof(GameServerConsole)} for server {id} while it was already connected.");
        }
        else if (consoleState == ConsoleState.Connecting)
        {
            logger.LogTrace($"Attempted to connect {nameof(GameServerConsole)} for server {id} while it was already connecting.");
        }
        else if (consoleState == ConsoleState.Disconnected)
        {
            consoleState = ConsoleState.Connecting;
            consoleClientFactoryTask = TryGetConsoleClientAsync();
        }
    }

    public async Task<ConsoleCommandResult<TResult>> RunConsoleCommandAsync<TResult>(ICommand<TResult> command)
    {
        return await this.TryWithConsole(
            clientAction: async (consoleClient) =>
            {
                var commandResult = await consoleClient.RunCommand(command.BuildCommandString(), TimeSpan.FromSeconds(30));

                if (commandResult.IsCompleted)
                {
                    var data = commandResult?.Message?.data ?? new JsonElement();
                    return new ConsoleCommandResult<TResult>(command.FromResponseJson(data));
                }

                throw new NotImplementedException("Poly needs to fix me.");
            },
            consoleNotAvailable: () =>
            {
                return Task.FromResult(ConsoleCommandResult<TResult>.AsConsoleNotAvailable());
                
            });
    }

    private async Task<TResult> TryWithConsole<TResult>(Func<ConsoleClient, Task<TResult>> clientAction, Func<Task<TResult>> consoleNotAvailable)
    {
        var clientTask = consoleClientFactoryTask;

        if (consoleState == ConsoleState.Connected)
        {
            var consoleClient = await clientTask.ConfigureAwait(false);

            return await clientAction(consoleClient!);
        }
        else
        {
            TryToConnect();

            var consoleClient = await consoleClientFactoryTask.ConfigureAwait(false);

            if (consoleClient == default)
            {
                return await consoleNotAvailable();
            }
            else
            {
                return await clientAction(consoleClient!);
            }
        }
    }

    private async Task<ConsoleClient?> TryGetConsoleClientAsync()
    {
        await semaphore.WaitAsync();

        if (consoleState == ConsoleState.Connected)
        {
            return await consoleClientFactoryTask;
        }

        try
        {
            var consoleClient = await CreateConsoleClientAsync();

            if (consoleClient != default)
            {
                consoleState = ConsoleState.Connected;
            }

            return consoleClient;
        }
        catch
        {
            consoleState = ConsoleState.Disconnected;
        }
        finally
        {
            semaphore.Release();
        }

        return default;
    }

    private async Task<ConsoleClient?> CreateConsoleClientAsync()
    {
        var access = await consoleAccessProvider.GetConsoleAccessAsync(id);
        if (access == ConsoleAccess.None)
        {
            return default;
        }
        else
        {
            try
            {
                var eventChannel = Channel.CreateUnbounded<Townsharp.Infrastructure.Consoles.Models.ConsoleEvent>();
                var client = consoleClientFactory.CreateClient(access.Uri, access.AccessToken, eventChannel.Writer);

                await client.ConnectAsync(default).ConfigureAwait(false);

                return client;
            }
            catch
            {
                return default;
            }
        }
    }
}

public enum ConsoleState
{
    Disconnected,
    Connecting,
    Connected
}

internal class UntypedLiteralConsoleCommand : ICommand<string>
{
    private string commandString;

    public UntypedLiteralConsoleCommand(string commandString)
    {
        this.commandString = commandString;
    }

    string ICommand<string>.BuildCommandString()
    {
        return commandString;
    }

    public string FromResponseJson(JsonElement responseJson)
    {
        return JsonObject.Create(responseJson)!.ToJsonString();
    }
}