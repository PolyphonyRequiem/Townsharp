//using System.Globalization;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;

//using Townsharp;
//using Townsharp.Infrastructure.WebApi.Models;

//namespace Tests.Townsharp.Infrastructure.WebApi;

//public class SerializationTests
//{
//    public class IdConverter<T> : JsonConverter<T>
//    {
//        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//        {
//            return (T) Activator.CreateInstance(typeToConvert, reader.GetInt32())!;
//        }

//        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
//        {
//            writer.WriteStringValue(value?.ToString());
//        }
//    }

//    public class SnakeCaseNamingPolicy : JsonNamingPolicy
//    {
//        public override string ConvertName(string name)
//        {
//            if (string.IsNullOrEmpty(name)) return name;

//            var builder = new StringBuilder();
//            for (int i = 0; i < name.Length; i++)
//            {
//                if (char.IsUpper(name[i]) && i > 0)
//                    builder.Append('_');
//                builder.Append(char.ToLowerInvariant(name[i]));
//            }
//            return builder.ToString();
//        }
//    }

//    [Fact]
//    public void GameServerStatusDeserialization()
//    {
//        // given this json response, I want to make sure that deserialization works correctly to GameServerStatus
//        var sampleJson = """
//           {
//               "id": 1174503463,
//               "name": "Cairnbrook",
//               "online_players": [
//                   {
//                       "id": 436473448,
//                       "username": "Friend"
//                   },
//                   {
//                       "id": 1696697783,
//                       "username": "LovedOne"
//                   },
//                   {
//                       "id": 73062789,
//                       "username": "Enemy"
//                   },
//                   {
//                       "id": 2141598626,
//                       "username": "ChaosGoblin"
//                   }
//               ],
//               "server_status": "Online",
//               "final_status": "Online",
//               "scene_index": 4,
//               "target": 2,
//               "region": "north-america-east",
//               "last_online": "2023-08-19T04:15:31.3507498Z",
//               "description": "A casual server for friendly players to explore, discover, and create.",
//               "playability": 2.0,
//               "version": "main-1.5.3.1.40518",
//               "group_id": 1156211297,
//               "owner_type": "Group",
//               "owner_id": 2026253269,
//               "type": "Normal",
//               "fleet": "att-quest",
//               "up_time": "13.00:16:19.5967831",
//               "join_type": "PrivateGroup",
//               "player_count": 4,
//               "created_at": "2021-09-10T20:34:29.2385673Z",
//               "is_online": true,
//               "transport_system": 1
//           }
//           """;

//        var serializerOptions = new JsonSerializerOptions
//        {
//            Converters =
//            {
//                new IdConverter<ServerId>(),
//                new IdConverter<GroupId>(),
//                new IdConverter<UserId>(),
//                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
//            },
//            PropertyNameCaseInsensitive = true,
//            PropertyNamingPolicy = new SnakeCaseNamingPolicy()
//        };

//        var server = JsonSerializer.Deserialize<ServerInfo>(sampleJson, serializerOptions)!;

//        Assert.Equal(1174503463, (int)server.Id);
//        Assert.Equal("Cairnbrook", server.Name);
//        Assert.Equal(4, server.OnlinePlayers.Length);
//        Assert.Collection(server.OnlinePlayers,
//              player => { Assert.Equal(436473448, (int)player.Id); Assert.Equal("Friend", player.Username); },
//              player => { Assert.Equal(1696697783, (int)player.Id); Assert.Equal("LovedOne", player.Username); },
//              player => { Assert.Equal(73062789, (int)player.Id); Assert.Equal("Enemy", player.Username); },
//              player => { Assert.Equal(2141598626, (int)player.Id); Assert.Equal("ChaosGoblin", player.Username); });              
//        Assert.Equal(GameServerStatus.Online, server.ServerStatus);
//        Assert.Equal(GameServerStatus.Online, server.FinalStatus);
//        Assert.Equal("north-america-east", server.Region);
//        Assert.Equal(2023, server.LastOnline?.Year);
//        Assert.Equal("A casual server for friendly players to explore, discover, and create.", server.Description);
//        Assert.Equal(2.0, server.Playability);
//        Assert.Equal(2026253269, (int) server.OwnerId);
//        Assert.Equal(ServerType.Normal, server.ServerType);
//        Assert.Equal(13, server.UpTime.Days);
//        Assert.Equal(ServerJoinType.PrivateGroup, server.JoinType);
//        Assert.Equal(4, server.PlayerCount);
//        Assert.Equal(2021, server.CreatedAt.Year);
//        Assert.True(server.IsOnline);
//        Assert.Equal(1156211297, (int)server.GroupId!);
//    }
//}
