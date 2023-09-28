// See https://aka.ms/new-console-template for more information
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.WebApi;

var httpClientFactory = new DefaultHttpClientFactory();

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole()); // disposes as scope terminates to flush logs.
var logger = loggerFactory.CreateLogger<Program>();

var username = Environment.GetEnvironmentVariable("TOWNSHARP_USERNAME");
var passwordHash = Environment.GetEnvironmentVariable("TOWNSHARP_PASSWORDHASH");

if (username == null || passwordHash == null)
{
    Console.WriteLine("Please set the TOWNSHARP_USERNAME and TOWNSHARP_PASSWORDHASH environment variables.");
    Console.WriteLine("Easy way to generate your hash using powershell can be found in .docs\\AltaContracts\\Identity\\Users.md");

    return;
}

// you probably don't want to use a bot for security reasons, but just in case, I've provided the means here.

//var botClientId = Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID");
//var botSecret = Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET");

//if (botClientId == null || botSecret == null)
//{
//    Console.WriteLine("Please set the TOWNSHARP_TEST_CLIENTID and TOWNSHARP_TEST_CLIENTSECRET environment variables.");

//    return;
//}

var userCreds = new UserCredential(username, passwordHash);
//var botCreds = new BotCredential(botClientId, botSecret);


IBotTokenProvider botTokenProvider = new DisabledBotTokenProvider();
// IBotTokenProvider botTokenProvider = new BotTokenProvider(botCreds, httpClientFactory.CreateClient());
//IUserTokenProvider userTokenProvider = new DisabledUserTokenProvider();
IUserTokenProvider userTokenProvider = new UserTokenProvider(userCreds, httpClientFactory.CreateClient()); // nevermind the lifetime of the client, this is just a repro.

var apiClient = new WebApiClient(botTokenProvider, userTokenProvider, httpClientFactory, loggerFactory.CreateLogger<WebApiClient>(), preferUserToken: true); // for user ID
//var apiClient = new WebApiClient(botTokenProvider, userTokenProvider, httpClientFactory, loggerFactory.CreateLogger<WebApiClient>());  // for bot ID

Console.WriteLine("Connect to what server?");
var serverIdString = Console.ReadLine();

int serverId = int.Parse(serverIdString!);

var accessResponse = await apiClient.RequestConsoleAccessAsync(serverId); // please disregard how this is implemented, it's on my list of things to rewrite.

UriBuilder uriBuilder = new UriBuilder();
string accessToken = "";

try
{
    if (!accessResponse["allowed"]?.GetValue<bool>() ?? false)
    {
        throw new InvalidOperationException("Server is not online.");
    }

    uriBuilder.Scheme = "ws";
    uriBuilder.Host = accessResponse["connection"]?["address"]?.GetValue<string>() ?? throw new Exception("Failed to get connection.address from response.");
    uriBuilder.Port = accessResponse["connection"]?["websocket_port"]?.GetValue<int>() ?? throw new Exception("Failed to get connection.host from response."); ;

    accessToken = accessResponse["token"]?.GetValue<string>() ?? throw new Exception("Failed to get token from response.");
}
catch (Exception ex)
{
    logger.LogError("Failed to get data required to console.  Likely offline.");
    logger.LogError(ex.ToString());
}

using var websocket = new ClientWebSocket();
await websocket.ConnectAsync(uriBuilder.Uri, CancellationToken.None);

logger.LogInformation("Connected to console, sending auth token normally");
await websocket.SendAsync(Encoding.UTF8.GetBytes(accessToken), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

var earlyRequestJsonString = JsonSerializer.Serialize(new { id = 1, content = "player list" })!;
logger.LogInformation("Sending a command message BEFORE we get our auth response..."); //  (for reasons, don't ask...)
await websocket.SendAsync(Encoding.UTF8.GetBytes(earlyRequestJsonString), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

// let's get the response to auth.
string authResponse = await GetMessageAsync(websocket);
logger.LogInformation($"Got response from auth request: {authResponse}");

string earlyCommandError = await GetMessageAsync(websocket);
logger.LogWarning($"Got response for the too-early command request: {earlyCommandError}");

// let's send a SINGLE command to demonstrate the issue.
logger.LogInformation("Sending a command message after we are fully auth'd...");
var commandJsonString = JsonSerializer.Serialize(new { id = 1, content = "player list" })!;
await websocket.SendAsync(Encoding.UTF8.GetBytes(commandJsonString), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

// One response...
string firstCommandResponse = await GetMessageAsync(websocket);
logger.LogInformation($"Got response for command (1st time): {firstCommandResponse}");

string secondCommandResponse = await GetMessageAsync(websocket);
logger.LogWarning($"Got response for command (2nd time): {secondCommandResponse}");

// They should differ only in timestamps.
websocket.Abort();

async Task<string> GetMessageAsync(ClientWebSocket websocket)
{
    byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 4);
    using MemoryStream ms = new MemoryStream();

    try
    {
        WebSocketReceiveResult result;
        using MemoryStream totalStream = new MemoryStream();

        do
        {
            try
            {
                // Use the rented buffer for receiving data
                result = await websocket.ReceiveAsync(new ArraySegment<byte>(rentedBuffer), CancellationToken.None).ConfigureAwait(false);

                // Message received, reset the idle timer
                // this.MarkLastMessageTime();
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debugger.Break();
                    break;
                }
            }
            catch (Exception)
            {
                Debugger.Break();
                break;
            }

            totalStream.Write(rentedBuffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                string rawMessage = Encoding.UTF8.GetString(totalStream.ToArray());
                totalStream.SetLength(0);

                return rawMessage;
            }
        } while (!result.EndOfMessage);
    }
    finally
    {
        // Return the buffer to the pool
        ArrayPool<byte>.Shared.Return(rentedBuffer);
    }

    throw new InvalidOperationException("Failed to get message.");
}

public sealed class DefaultHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly Lazy<HttpMessageHandler> _handlerLazy = new(() => new HttpClientHandler());

    public HttpClient CreateClient(string name) => new(_handlerLazy.Value, disposeHandler: false);

    public void Dispose()
    {
        if (_handlerLazy.IsValueCreated)
        {
            _handlerLazy.Value.Dispose();
        }
    }
}
