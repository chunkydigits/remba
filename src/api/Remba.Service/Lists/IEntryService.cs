using Remba.Model.Lists;

namespace Remba.Service.Lists;

public interface IEntryService
{
    Task<EntryDto> CreateAsync(
        string ownerId,
        string listId,
        CreateEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntryDto>> GetByListAsync(
        string ownerId,
        string listId,
        EntryQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<EntryDto?> GetOwnAsync(
        string ownerId,
        string listId,
        string entryId,
        CancellationToken cancellationToken = default);

    Task<EntryDto> UpdateAsync(
        string ownerId,
        string listId,
        string entryId,
        UpdateEntryRequest request,
        CancellationToken cancellationToken = default);

    Task ReorderAsync(
        string ownerId,
        string listId,
        ReorderEntriesRequest request,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        string ownerId,
        string listId,
        string entryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetListIdsContainingAsync(
        string ownerId,
        string? identifierType,
        string? identifierValue,
        string? entityId,
        CancellationToken cancellationToken = default);
}
