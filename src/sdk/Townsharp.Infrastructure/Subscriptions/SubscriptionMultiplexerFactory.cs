using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Composition;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.Subscriptions;

/// <summary>
/// Factory for creating <see cref="SubscriptionMultiplexer"/> instances.
/// </summary>
public class SubscriptionMultiplexerFactory
{
    private const int DEFAULT_MAX_CONNECTIONS = 10;

    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionMultiplexerFactory"/> class, using the default <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <param name="botCredential">The <see cref="BotCredential"/> to use for authentication.</param>
    public SubscriptionMultiplexerFactory(BotCredential botCredential)
        : this(new SubscriptionClientFactory(new BotTokenProvider(botCredential), InternalLoggerFactory.Default), InternalLoggerFactory.Default)
    {
    }

    internal SubscriptionMultiplexerFactory(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new <see cref="SubscriptionMultiplexer"/> instance, using the provided <paramref name="concurrentConnections"/> to limit the number of concurrent connections.
    /// </summary>
    /// <param name="concurrentConnections">The maximum number of concurrent connections to use.  The default is 10.  As a guideline, I would suggest no less than 1 connection per 500 distinct subscriptions.</param>
    public SubscriptionMultiplexer Create(int concurrentConnections = DEFAULT_MAX_CONNECTIONS)
    {
        if (concurrentConnections is > 50 or < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrentConnections), "The number of concurrent connections must be between 1 and 50.");
        }

        return SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);
    }
}
