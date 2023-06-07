﻿using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Townsharp.Identity;
using Townsharp.Infrastructure.Identity.Models;
using Townsharp.Infrastructure.Subscriptions;

Console.WriteLine("Starting a SubscriptionConnection test.");

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Services.AddHttpClient();
builder.Services.AddSingleton(
    services => new BotTokenProvider(
           new BotCredential(
               Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTID")!,
               Environment.GetEnvironmentVariable("TOWNSHARP_TEST_CLIENTSECRET")!),
           services.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton<SubscriptionClientFactory>();

builder.Logging.AddConsole();
//builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddHostedService<SubscriptionConnectionTest>();

IHost host = builder.Build();

host.Run();

internal class SubscriptionConnectionTest : IHostedService
{
    private readonly BotTokenProvider botTokenProvider;
    private readonly ILogger<SubscriptionConnectionTest> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private SubscriptionConnection? subscriptionConnection;

    public SubscriptionConnectionTest(
        BotTokenProvider botTokenProvider, 
        ILogger<SubscriptionConnectionTest> logger, 
        ILoggerFactory loggerFactory, 
        SubscriptionClientFactory subscriptionClientFactory)
    {
        this.botTokenProvider = botTokenProvider;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.subscriptionClientFactory = subscriptionClientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.subscriptionConnection = await SubscriptionConnection.CreateAsync(this.subscriptionClientFactory, this.loggerFactory.CreateLogger<SubscriptionConnection>());

        _ = Task.Run(() => SubscribeToGroupsAsync(cancellationToken), cancellationToken);

        this.subscriptionConnection.OnSubscriptionEvent += (sender, subscriptionEvent) =>
        {
            logger.LogInformation($"Received Event - {subscriptionEvent.EventId}/{subscriptionEvent.KeyId} - {subscriptionEvent.Content.GetRawText()}");
        };
    }
    
    private async Task SubscribeToGroupsAsync(CancellationToken cancellationToken)
    {
        var groupIds = await this.GetJoinedGroupIdsAsync(cancellationToken);
        this.subscriptionConnection!.Subscribe(groupIds.Take(500).Select(id => new SubscriptionDefinition("group-server-heartbeat", id)).ToArray());
    }

    private async Task<long[]> GetJoinedGroupIdsAsync(CancellationToken cancellationToken)
    {
        var apiClient = new HttpClient();
        apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {await this.botTokenProvider.GetTokenAsync(CancellationToken.None)}");
        var joinedGroups = await apiClient.GetFromJsonAsync<JsonDocument>("https://webapi.townshiptale.com/api/groups/joined?limit=2000", cancellationToken);
        var groupsIds = joinedGroups!.RootElement.EnumerateArray().Select(g => g.GetProperty("group").GetProperty("id").GetInt64()).ToArray();

        return groupsIds;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            Environment.Exit(0);
        }

        await this.subscriptionConnection!.DisposeAsync();
    }
}