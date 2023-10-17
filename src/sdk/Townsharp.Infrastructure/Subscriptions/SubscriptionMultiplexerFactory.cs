using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Composition;
using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Identity;

namespace Townsharp.Infrastructure.Subscriptions;

public class SubscriptionMultiplexerFactory
{
    private const int DEFAULT_MAX_CONNECTIONS = 10;

    private readonly SubscriptionClientFactory subscriptionClientFactory;
    private readonly ILoggerFactory loggerFactory;

    public SubscriptionMultiplexerFactory()
        : this(new SubscriptionClientFactory(InternalBotTokenProvider.Default, InternalLoggerFactory.Default), InternalLoggerFactory.Default)
    {
    }

    public SubscriptionMultiplexerFactory(BotCredential botCredential)
        : this(new SubscriptionClientFactory(new BotTokenProvider(botCredential, InternalHttpClientFactory.Default), InternalLoggerFactory.Default), InternalLoggerFactory.Default)
    {
    }

    internal SubscriptionMultiplexerFactory(SubscriptionClientFactory subscriptionClientFactory, ILoggerFactory loggerFactory)
    {
        this.subscriptionClientFactory = subscriptionClientFactory;
        this.loggerFactory = loggerFactory;
    }

    public SubscriptionMultiplexer Create(int concurrentConnections = DEFAULT_MAX_CONNECTIONS)
    {
        return SubscriptionMultiplexer.Create(this.subscriptionClientFactory, this.loggerFactory, concurrentConnections);
    }
}
