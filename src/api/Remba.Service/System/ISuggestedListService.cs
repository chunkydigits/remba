using Remba.Model.System;

namespace Remba.Service.System;

public interface ISuggestedListService
{
    /// <summary>
    /// Creates any missing built-in suggested lists. Existing documents are left unchanged.
    /// </summary>
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SuggestedListDto>> GetAllAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);

    Task<SuggestedListDto?> GetBySeedKeyAsync(string seedKey, CancellationToken cancellationToken = default);

    Task<SuggestedListDto> UpdateAsync(
        string seedKey,
        UpdateSuggestedListRequest request,
        CancellationToken cancellationToken = default);
}
