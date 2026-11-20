using Microsoft.Extensions.Logging;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public class EntityLookupService : IEntityLookupService
{
    private readonly IEnumerable<IEntityLookupProvider> _providers;
    private readonly ILogger<EntityLookupService> _logger;

    public EntityLookupService(
        IEnumerable<IEntityLookupProvider> providers,
        ILogger<EntityLookupService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EntitySearchResultDto>> SearchAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalisedType = StringUtilities.NormalizeWhitespace(entityType).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalisedType))
        {
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        }

        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedQuery))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        var provider = _providers.FirstOrDefault(item => item.SupportsEntityType(normalisedType));
        if (provider is null)
        {
            throw new ArgumentException($"No search provider is registered for entity type '{normalisedType}'.");
        }

        _logger.LogDebug(
            "Searching entities via {Provider} for type {EntityType}",
            provider.ProviderKey,
            normalisedType);

        return await provider.SearchAsync(normalisedType, normalisedQuery, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EntitySearchResultDto?> GetByIdentifierAsync(
        string entityType,
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        var normalisedType = StringUtilities.NormalizeWhitespace(entityType).ToLowerInvariant();
        var normalisedIdentifierType = StringUtilities.NormalizeWhitespace(identifierType);
        var normalisedIdentifierValue = StringUtilities.NormalizeWhitespace(identifierValue);

        if (string.IsNullOrWhiteSpace(normalisedType)
            || string.IsNullOrWhiteSpace(normalisedIdentifierType)
            || string.IsNullOrWhiteSpace(normalisedIdentifierValue))
        {
            return null;
        }

        var provider = _providers.FirstOrDefault(item => item.SupportsEntityType(normalisedType));
        if (provider is null)
        {
            return null;
        }

        return await provider
            .GetByIdentifierAsync(
                normalisedType,
                normalisedIdentifierType,
                normalisedIdentifierValue,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
