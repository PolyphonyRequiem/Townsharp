using System.Text.Json.Serialization;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.WebApi;

// Shared Models
[JsonSerializable(typeof(UserInfo))]

// Root Success Responses
[JsonSerializable(typeof(ConsoleAccess))]
[JsonSerializable(typeof(ConsoleConnectionInfo))]

internal partial class WebApiSerializerContext : JsonSerializerContext
{

}
