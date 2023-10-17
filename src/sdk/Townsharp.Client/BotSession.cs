using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Client;

public class BotSession
{
    private readonly BotCredential credential;
    private readonly BotClientSessionConfiguration configuration;

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILoggerFactory loggerFactory;

    private readonly WebApiBotClient webApiClient;
    private readonly SubscriptionMultiplexer subscriptionMultiplexer;
    private readonly ConsoleClientFactory consoleClientFactory;

    private Action<ManagedServer> handleServerAdded = _ => { };

    public void HandleServerAdded(Action<ManagedServer> action) => this.handleServerAdded = action;
    
    private Action<ManagedServer> handleServerRemoved = _ => { };

    public void HandleServerRemoved(Action<ManagedServer> action) => this.handleServerRemoved = action;

    private Action<ManagedGroup> handleGroupAdded = _ => { };

    public void HandleGroupAdded(Action<ManagedGroup> action) => this.handleGroupAdded = action;
    
    private Action<ManagedGroup> handleGroupRemoved = _ => { };

    public void HandleGroupRemoved(Action<ManagedGroup> action) => this.handleGroupRemoved = action;

    // should probably move half of these out into the factory.
    internal BotSession(
        BotCredential credential, 
        BotClientSessionConfiguration configuration, 
        IHttpClientFactory httpClientFactory, 
        ILoggerFactory loggerFactory)
    {
        this.credential = credential;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
        this.loggerFactory = loggerFactory;

        var botTokenProvider = new BotTokenProvider(credential, httpClientFactory);
        var subscriptionMultiplexerFactory = new SubscriptionMultiplexerFactory(
            new SubscriptionClientFactory(botTokenProvider, loggerFactory),
            loggerFactory);

        this.webApiClient = new WebApiBotClient(botTokenProvider, httpClientFactory, loggerFactory.CreateLogger<WebApiBotClient>());      
        this.subscriptionMultiplexer = subscriptionMultiplexerFactory.Create(this.configuration.MaxSubscriptionWebsockets);
        this.consoleClientFactory = new ConsoleClientFactory(loggerFactory);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (this.configuration.AutoAcceptInvites)
        {
            // Set up Auto Accept Invites
        }

        if (this.configuration.AutoManageGroupsAndServers)
        {
            // Setup AutomanageGroupsAndServers
            // await management complete.
        }
    }
}
