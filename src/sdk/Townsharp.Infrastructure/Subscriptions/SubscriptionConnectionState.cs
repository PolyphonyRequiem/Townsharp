namespace Townsharp.Infrastructure;

internal enum SubscriptionConnectionState
{
    Created,
    MigrationTokenAcquired,
    Faulted,
    ConnectedReady
}

// I need a simple state machine that can be used to track the state of the subscription connection.
// I don't know all the state transition rules yet so for now this should be just a simple state machine stub.
// Needs to be thread safe.

internal class SubscriptionConnectionStateMachine
{
    private SubscriptionConnectionState state;

    public SubscriptionConnectionStateMachine()
    {
        this.state = SubscriptionConnectionState.Created;
    }

    public SubscriptionConnectionState State => this.state;

    public void TransitionTo(SubscriptionConnectionState newState)
    {
        this.state = newState;
    }
}

