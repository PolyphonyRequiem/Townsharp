using Townsharp;
using Townsharp.Servers;

namespace Tests.Townsharp.Core.Servers;

public class ServerEventTests
{
    [Fact]
    public void PopulationChangedEvent_CorrectlyIdentifiesChanges()
    {
        // test that when Server's population changes, a PopulationChangedEvent is raised that correctly identifies changes in population.

        Server s = new(1, 2);

        List<PopulationChangedEvent> populationChangedEvents = new();

        s.PopulationChanged += (s, e) =>
        {
            populationChangedEvents.Add(e);
        };

        var player1 = new UserInfo(1, "a");
        var player2 = new UserInfo(2, "b");
        var player3 = new UserInfo(3, "c");
        var player4 = new UserInfo(4, "d");
        var player5 = new UserInfo(5, "e");

        // Initial Users
        var initialUsers = new UserInfo[] { player1, player2 };
        s.UpdatePopulation(initialUsers);

        var updatedUsers = new UserInfo[] { player1, player3, player4, player5 };
        s.UpdatePopulation(updatedUsers);

        Assert.Equal(2, populationChangedEvents.Count);

        var firstEvent = populationChangedEvents[0];
        Assert.Collection(
            firstEvent.JoinedPlayers,
            u => Assert.Equal(player1, u),
            u => Assert.Equal(player2, u));
        Assert.Empty(firstEvent.LeftPlayers);

        var secondEvent = populationChangedEvents[1];
        Assert.Collection(
            secondEvent.JoinedPlayers,
            u => Assert.Equal(player3, u),
            u => Assert.Equal(player4, u),
            u => Assert.Equal(player5, u));
        Assert.Collection(
            secondEvent.LeftPlayers,
            u => Assert.Equal(player2, u));
    }

    [Fact]
    public void ServerOnlineEvent_RaisedWhenServerComesOnline()
    {
        // test that when Server comes online, a ServerOnlineEvent is raised.
        Server s = new(1, 2);

        s.ServerOnline += (s, e) =>
        {
            Assert.True(true);
        };

        s.SetOnline();
    }

    [Fact]
    public void ServerOfflineEvent_RaisedWhenServerGoesOffline()
    {
        // test that when Server goes offline, a ServerOfflineEvent is raised.
        Server s = new(1, 2);

        s.ServerOffline += (s, e) =>
        {
            Assert.True(true);
        };

        s.SetOffline();
    }
}
