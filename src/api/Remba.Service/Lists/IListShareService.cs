using Remba.Model.Lists;
using Remba.Model.Users;

namespace Remba.Service.Lists;

public interface IListShareService
{
    Task<ListAccessContext?> TryResolveAccessAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<ListAccessContext> RequireAccessAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListShareMemberDto>> GetMembersAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListShareMemberDto>> ShareAsync(
        string userId,
        string listId,
        ShareListRequest request,
        CancellationToken cancellationToken = default);

    Task UnshareAsync(
        string userId,
        string listId,
        string memberUserId,
        CancellationToken cancellationToken = default);

    Task LeaveAsync(string userId, string listId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListShareDbo>> GetSharesForMemberAsync(
        string memberUserId,
        CancellationToken cancellationToken = default);

    Task RemoveAllSharesForListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default);

    Task SyncListNameAsync(
        string listOwnerId,
        string listId,
        string listName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants membership to an already-resolved user (e.g. invite-code redeem).
    /// </summary>
    Task GrantShareAsync(
        ListDbo list,
        UserDto member,
        string role,
        string grantedByUserId,
        string? shareLabel = null,
        CancellationToken cancellationToken = default);
}
