using Remba.Model.Lists;

namespace Remba.Repository.Lists;

public interface IEntryRepository
{
    Task<EntryDbo?> GetByIdAsync(string ownerId, string entryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntryDbo>> GetByListAsync(
        string ownerId,
        string listId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<EntryDbo?> FindActiveByEntityAsync(
        string ownerId,
        string listId,
        string entityId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextOrderAsync(string ownerId, string listId, CancellationToken cancellationToken = default);

    Task<EntryDbo> CreateAsync(EntryDbo entry, CancellationToken cancellationToken = default);

    Task<EntryDbo> UpdateAsync(EntryDbo entry, CancellationToken cancellationToken = default);
}
