using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;

namespace Townsharp.Client;

internal class SessionFactory
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILoggerFactory loggerFactory;

    public SessionFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        this.httpClientFactory = httpClientFactory;
        this.loggerFactory = loggerFactory;
    }

    internal BotSession CreateBotSession(BotCredential credential, BotClientSessionConfiguration configuration)
    {
        return new BotSession(credential, configuration, this.httpClientFactory, this.loggerFactory);
    }
}