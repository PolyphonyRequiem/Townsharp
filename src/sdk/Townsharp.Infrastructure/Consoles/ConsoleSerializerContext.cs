using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Townsharp.Infrastructure.Models;
using Townsharp.Infrastructure.Consoles;

namespace Townsharp.Infrastructure.Subscriptions;

// Console Events
[JsonSerializable(typeof(PlayerStateChangedEvent))]
[JsonSerializable(typeof(PlayerJoinedEvent))]
[JsonSerializable(typeof(PlayerLeftEvent))]
[JsonSerializable(typeof(PlayerMovedChunkEvent))]

// Wire Messages
[JsonSerializable(typeof(ConsoleMessage))]
[JsonSerializable(typeof(CommandResponseMessage))]
[JsonSerializable(typeof(ConsoleSubscriptionEventMessage))]

// Request Message
[JsonSerializable(typeof(CommandRequestMessage))]

// Content
[JsonSerializable(typeof(UserInfo))]
internal partial class ConsoleSerializerContext : JsonSerializerContext
{

}

internal class Vector3Converter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = 0;
        var y = 0;
        var z = 0;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            x = reader.GetInt32();
            reader.Read();
            y = reader.GetInt32();
            reader.Read();
            z = reader.GetInt32();
            reader.Read();
        }

        return new Vector3(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}