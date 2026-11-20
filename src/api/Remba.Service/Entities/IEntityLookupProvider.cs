using Remba.Model.Entities;

namespace Remba.Service.Entities;

public interface IEntityLookupProvider
{
    string ProviderKey { get; }

    bool SupportsEntityType(string entityType);

    Task<IReadOnlyList<EntitySearchResultDto>> SearchAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a single known identifier to a rich result (used when adding from search).
    /// </summary>
    Task<EntitySearchResultDto?> GetByIdentifierAsync(
        string entityType,
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default);
}
