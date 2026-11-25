using Remba.Model.Lists;

namespace Remba.Repository.Lists;

public interface IListShareRepository
{
    Task<ListShareDbo?> GetAsync(
        string partitionOwnerId,
        string shareId,
        CancellationToken cancellationToken = default);

    Task<ListShareDbo?> GetForMemberAsync(
        string memberUserId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListShareDbo>> GetByListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListShareDbo>> GetByMemberAsync(
        string memberUserId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(ListShareDbo share, CancellationToken cancellationToken = default);

    Task DeleteAsync(string partitionOwnerId, string shareId, CancellationToken cancellationToken = default);
}
