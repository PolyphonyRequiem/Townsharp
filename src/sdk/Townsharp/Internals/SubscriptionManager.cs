using MediatR;

using Townsharp.Infrastructure.Subscriptions;

namespace Townsharp.Internals;

internal class SubscriptionManager
{
    private readonly IMediator mediator;
    private readonly SubscriptionMultiplexerFactory subscriptionMultiplexerFactory;

    private readonly Task<SubscriptionMultiplexer> subscriptionMultiplexerTask;

    public SubscriptionManager(IMediator mediator, SubscriptionMultiplexerFactory subscriptionMultiplexerFactory)
    {
        this.mediator = mediator;
        this.subscriptionMultiplexerFactory = subscriptionMultiplexerFactory;
        this.subscriptionMultiplexerTask = this.InitializeAsync();
    }

    private async Task<SubscriptionMultiplexer> InitializeAsync()
    {
        return await subscriptionMultiplexerFactory.CreateAsync();
    }

    private async void F()
    {
        var multiplexer = await subscriptionMultiplexerTask.ConfigureAwait(false);
        ulong userId = 0; // get from JWT claims
        multiplexer.RegisterSubscriptions(new[] { new SubscriptionDefinition("me-group-invite-create", (long) userId) });
    }
}
