namespace Townsharp.Servers;

public record PopulationChangedEvent(UserInfo[] JoinedPlayers, UserInfo[] LeftPlayers)
{
}