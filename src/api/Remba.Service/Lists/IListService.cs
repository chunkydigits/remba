using Remba.Model.Lists;

namespace Remba.Service.Lists;

public interface IListService
{
    Task<ListDto> CreateAsync(string ownerId, CreateListRequest request, CancellationToken cancellationToken = default);

    Task<ListDto?> GetOwnAsync(string ownerId, string listId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListSummaryDto>> GetOwnListsAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<ListDto> UpdateAsync(string ownerId, string listId, UpdateListRequest request, CancellationToken cancellationToken = default);

    Task<ListDto> UpdateViewSettingsAsync(
        string ownerId,
        string listId,
        UpdateListViewSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default);
}
