using MediatR;

using Townsharp.Infrastructure.Subscriptions;
using Townsharp.Internals.Sessions.Requests;

namespace Townsharp.Internals.Subscriptions.Handlers;

internal class AutoAcceptInvitationsRequestHandler : IRequestHandler<AutoAcceptInvitationsRequest>
{
    public AutoAcceptInvitationsRequestHandler()
    {

    }

    public async Task Handle(AutoAcceptInvitationsRequest request, CancellationToken cancellationToken)
    {

    }
}
