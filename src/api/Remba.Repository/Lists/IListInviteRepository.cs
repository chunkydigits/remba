using Remba.Model.Lists;

namespace Remba.Repository.Lists;

public interface IListInviteRepository
{
    Task<ListInviteDbo?> GetByIdAsync(
        string listOwnerId,
        string inviteId,
        CancellationToken cancellationToken = default);

    Task<ListInviteDbo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListInviteDbo>> GetByListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<ListInviteDbo> CreateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default);

    Task<ListInviteDbo> UpdateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default);

    Task DeleteAsync(string listOwnerId, string inviteId, CancellationToken cancellationToken = default);
}
