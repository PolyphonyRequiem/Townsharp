using Townsharp.Infrastructure.Models;

namespace Townsharp.Infrastructure.WebApi;
public interface IWebApiClient
{
   Task<WebApiResult<GroupMemberInfo>> AcceptGroupInviteAsync(int groupId);

   Task<WebApiResult<GroupInfoDetailed>> GetGroupAsync(int groupId);

   Task<IEnumerable<JoinedGroupInfo>> GetJoinedGroupsAsync();

   IAsyncEnumerable<JoinedGroupInfo> GetJoinedGroupsAsyncStream();

   Task<IEnumerable<ServerInfo>> GetJoinedServersAsync();

   IAsyncEnumerable<ServerInfo> GetJoinedServersAsyncStream();

   IAsyncEnumerable<InvitedGroupInfo> GetPendingGroupInvitationsAsyncStream();

   Task<IEnumerable<InvitedGroupInfo>> GetPendingGroupInvitesAsync();

   Task<WebApiResult<ServerInfo>> GetServerAsync(int serverId);

   Task<WebApiResult<ConsoleAccess>> RequestConsoleAccessAsync(int serverId);
}