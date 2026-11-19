using Remba.Model.Lists;

namespace Remba.Repository.Lists;

public interface IListRepository
{
    Task<ListDbo?> GetByIdAsync(string ownerId, string listId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListDbo>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<ListDbo> CreateAsync(ListDbo list, CancellationToken cancellationToken = default);

    Task<ListDbo> UpdateAsync(ListDbo list, CancellationToken cancellationToken = default);

    Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default);
}
