namespace Townsharp.Client;

public class BotClientSessionConfiguration
{
    public int MaxSubscriptionWebsockets { get; init; } = 1;

    public bool AutoManageGroupsAndServers { get; init; } = true;

    public bool AutoAcceptInvites { get; init; } = true;
}