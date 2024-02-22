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
        float x = 0;
        float y = 0;
        float z = 0;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            x = reader.GetSingle();
            reader.Read();
            y = reader.GetSingle();
            reader.Read();
            z = reader.GetSingle();
            reader.Read();
        }

        return new Vector3(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}