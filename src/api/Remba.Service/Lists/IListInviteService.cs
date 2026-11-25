using Remba.Model.Lists;

namespace Remba.Service.Lists;

public interface IListInviteService
{
    Task<IReadOnlyList<ListInviteDto>> GetByListAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<ListInviteDto> CreateAsync(
        string userId,
        string listId,
        CreateListInviteRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string userId,
        string listId,
        string inviteId,
        CancellationToken cancellationToken = default);

    Task<RedeemListInviteResponse> RedeemAsync(
        string userId,
        RedeemListInviteRequest request,
        CancellationToken cancellationToken = default);
}
