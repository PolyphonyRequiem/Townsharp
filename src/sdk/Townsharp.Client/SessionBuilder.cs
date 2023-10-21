using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;

namespace Townsharp.Client;

public class SessionBuilder
{
    private readonly ServiceCollection services;
    private readonly IConfigurationBuilder configurationBuilder;

    protected SessionBuilder(string environment = "")
    {
        this.services = new ServiceCollection();

        this.configurationBuilder = new ConfigurationBuilder();

        this.configurationBuilder.AddJsonFile("appsettings.json", optional: true);

        if (environment != "")
        {
            this.configurationBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true);
        }
    }

    public static SessionBuilder Create()
    {
        return new SessionBuilder();
    }

    public SessionBuilder AddLogging()
    {
        this.services.AddLogging();
        return this;
    }

    public SessionBuilder AddLogging(Action<ILoggingBuilder> configureLogging)
    {
        this.services.AddLogging(configureLogging);
        return this;
    }

    public BotSession CreateBotSession() => this.CreateBotSession(BotCredential.FromEnvironmentVariables(), CreateDefaultConfiguration());

    public BotSession CreateBotSession(BotCredential credential) => CreateBotSession(credential, CreateDefaultConfiguration());

    public BotSession CreateBotSession(BotClientSessionConfiguration configuration) => this.CreateBotSession(BotCredential.FromEnvironmentVariables(), configuration);

    public BotSession CreateBotSession(BotCredential credential, BotClientSessionConfiguration configuration)
    {
        var provider = this.services.BuildServiceProvider();
        var factory = provider.GetRequiredService<SessionFactory>();

        return factory.CreateBotSession(credential, configuration);
    }

    private static BotClientSessionConfiguration CreateDefaultConfiguration() => new BotClientSessionConfiguration();
}
