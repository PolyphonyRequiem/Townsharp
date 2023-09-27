using System.Text.Json;
using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Subscriptions.Models;

namespace Tests.Townsharp.Infrastructure.Subscriptions;

public class SerializationTests
{
    [Fact]
    public void HeartbeatEventSerializationTest()
    {
        string messageJson = """
        {
            "id": 1,
            "event": "response",
            "key": "POST /ws/subscription/group-server-heartbeat/1918420763",
            "content": "",
            "responseCode": 200
        }
        """;

        var message = JsonSerializer.Deserialize<SubscriptionMessage>(messageJson, AltaSerializerContext.Default.SubscriptionMessage);

        Assert.NotNull(message);
        Assert.Equal(1, message!.id);
        Assert.Equal("response", message.@event);
    }

    [Fact]
    public void SubscriptionEventSerializationTest()
    {
        string messageJson = """
        {
            "id": 0,
            "event": "group-server-heartbeat",
            "key": "1708787179",
            "content": "{\"id\":1053784199,\"name\":\"Lost Soulz\",\"online_players\":[{\"id\":616483123,\"username\":\"anubis-\"},{\"id\":603425874,\"username\":\"Digital_\"},{\"id\":1812752464,\"username\":\"-BeautifulSoul-\"},{\"id\":1608021436,\"username\":\"llama2012\"},{\"id\":137015387,\"username\":\"-Chem-\"},{\"id\":188362704,\"username\":\"-Bee-\"},{\"id\":133827123,\"username\":\"mellyboy\"},{\"id\":1992372140,\"username\":\"femboykisser346\"}],\"server_status\":\"Online\",\"final_status\":\"Online\",\"scene_index\":4,\"target\":2,\"region\":\"australia-agones\",\"online_ping\":\"2023-09-18T05:35:28.799191Z\",\"last_online\":\"2023-09-18T05:35:28.799191Z\",\"description\":\"Welcome to Lost Soulz!\\nWeekly Events/ Discord Challenges\\nAlchemy/ Active Admins\\nLGBTQ+ Friendly\\nInvis skins are illegal\\nCome join the fun on Discord!\\ndiscord.gg/vtR9d3NvBs\",\"playability\":0.0,\"version\":\"main-1.6.0.0.40940\",\"group_id\":1708787179,\"owner_type\":\"Group\",\"owner_id\":1812752464,\"type\":\"Normal\",\"fleet\":\"att-quest\",\"up_time\":\"33.17:26:55.2693549\",\"join_type\":\"OpenGroup\",\"player_count\":8,\"created_at\":\"2021-10-31T00:03:18.3603207Z\",\"is_online\":true,\"transport_system\":1}",
            "responseCode": 200
        }
        """;

        var message = JsonSerializer.Deserialize<SubscriptionEventMessage>(messageJson, AltaSerializerContext.Default.SubscriptionEventMessage);

        Assert.NotNull(message);
        Assert.Equal("group-server-heartbeat", message!.@event);
        Assert.Equal("1708787179", message.key);

        var content = message.content;
        var contentJson = message.content;
    }

    [Fact]
    public void GroupServerHeartbeatSerializationTest()
    {
        string messageJson = """
        {
            "id": 1053784199,
            "name": "Lost Soulz",
            "online_players": [
                {
                    "id": 616483123,
                    "username": "anubis-"
                },
                {
                    "id": 603425874,
                    "username": "Digital_"
                },
                {
                    "id": 1812752464,
                    "username": "-BeautifulSoul-"
                },
                {
                    "id": 1608021436,
                    "username": "llama2012"
                },
                {
                    "id": 137015387,
                    "username": "-Chem-"
                },
                {
                    "id": 188362704,
                    "username": "-Bee-"
                },
                {
                    "id": 133827123,
                    "username": "mellyboy"
                },
                {
                    "id": 1992372140,
                    "username": "femboykisser346"
                }
            ],
            "server_status": "Online",
            "final_status": "Online",
            "scene_index": 4,
            "target": 2,
            "region": "australia-agones",
            "online_ping": "2023-09-18T05:35:28.799191Z",
            "last_online": "2023-09-18T05:35:28.799191Z",
            "description": "Welcome to Lost Soulz!\nWeekly Events/ Discord Challenges\nAlchemy/ Active Admins\nLGBTQ+ Friendly\nInvis skins are illegal\nCome join the fun on Discord!\ndiscord.gg/vtR9d3NvBs",
            "playability": 0.0,
            "version": "main-1.6.0.0.40940",
            "group_id": 1708787179,
            "owner_type": "Group",
            "owner_id": 1812752464,
            "type": "Normal",
            "fleet": "att-quest",
            "up_time": "33.17:26:55.2693549",
            "join_type": "OpenGroup",
            "player_count": 8,
            "created_at": "2021-10-31T00:03:18.3603207Z",
            "is_online": true,
            "transport_system": 1
        }
        """;

        var message = JsonSerializer.Deserialize<ServerStatusContent>(messageJson, AltaSerializerContext.Default.GroupServerHeartbeatContent);

        Assert.NotNull(message);
        Assert.Equal(1053784199, message!.id);
        Assert.Equal("Lost Soulz", message.name);
        Assert.Equal(8, message.online_players.Length);
        Assert.Equal("Online", message.server_status);
        Assert.Equal("Online", message.final_status);
        Assert.Equal(4, message.scene_index);
        Assert.Equal(2, message.target);
        Assert.Equal("australia-agones", message.region);
        Assert.Equal("Welcome to Lost Soulz!\nWeekly Events/ Discord Challenges\nAlchemy/ Active Admins\nLGBTQ+ Friendly\nInvis skins are illegal\nCome join the fun on Discord!\ndiscord.gg/vtR9d3NvBs", message.description);
        Assert.Equal(0.0f, message.playability);
        Assert.Equal("main-1.6.0.0.40940", message.version);
        Assert.Equal(1708787179, message.group_id);
        Assert.Equal("Group", message.owner_type);
        Assert.Equal(1812752464, message.owner_id);
        Assert.Equal("Normal", message.type);
        Assert.Equal("att-quest", message.fleet);
        Assert.Equal(TimeSpan.Parse("33.17:26:55.2693549"), message.up_time);
        Assert.Equal("OpenGroup", message.join_type);
        Assert.Equal(8, message.player_count);
        Assert.True(message.is_online);
        Assert.Equal(1, message.transport_system);
    }

    [Fact]
    public void SanityCheck()
    {
        string insanity = """
            {"id":37328628,"name":"stormwind","online_players":[{"id":916558912,"username":"Oakmocha"},{"id":1974571506,"username":"xotti"},{"id":765910433,"username":"Ideafix"},{"id":1738853324,"username":"TheDooby"},{"id":981959701,"username":"D_Danger007"},{"id":2062584417,"username":"Dallan"}],"server_status":"Online","final_status":"Online","scene_index":4,"target":2,"region":"north-america-east","online_ping":"2023-09-24T01:05:07.0133238Z","last_online":"2023-09-24T01:05:07.0133238Z","description":"An amazing server nhttps://discord.gg/keSXWHrFvMn","playability":0.0,"version":"main-1.6.0.0.40940","group_id":1983084595,"owner_type":"Group","owner_id":690950185,"type":"Normal","fleet":"att-quest","up_time":"1.04:05:26.1240756","join_type":"OpenGroup","player_count":6,"player_limit":8,"created_at":"2022-01-23T20:29:06.5447904Z","is_online":true,"transport_system":1}
            """;

        var element = JsonDocument.Parse(insanity).RootElement;

        var message = JsonSerializer.Deserialize<ServerStatusContent>(element, AltaSerializerContext.Default.GroupServerHeartbeatContent);

        Assert.NotNull(message);
    }
}


[JsonSerializable(typeof(ServerStatusContent))]
[JsonSerializable(typeof(SubscriptionEventMessage))]
[JsonSerializable(typeof(SubscriptionMessage))]
public partial class AltaSerializerContext : JsonSerializerContext
{

}