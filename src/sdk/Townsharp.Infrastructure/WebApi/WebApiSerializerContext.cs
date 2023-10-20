using System.Text.Json.Serialization;

using Townsharp.Infrastructure.CommonModels;

namespace Townsharp.Infrastructure.WebApi;

// Shared Models
[JsonSerializable(typeof(UserInfo))]

// Root Success Responses
[JsonSerializable(typeof(ConsoleAccess))]
[JsonSerializable(typeof(ConsoleConnectionInfo))]
[JsonSerializable(typeof(ServerInfo))]
[JsonSerializable(typeof(JoinedGroupInfo))]
[JsonSerializable(typeof(GroupInfoDetailed))]
[JsonSerializable(typeof(GroupServerInfo))]
[JsonSerializable(typeof(GroupRoleInfo))]
[JsonSerializable(typeof(GroupMemberInfo))]
[JsonSerializable(typeof(InvitedGroupInfo))]

internal partial class WebApiSerializerContext : JsonSerializerContext
{

}
