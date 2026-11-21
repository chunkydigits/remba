using Microsoft.Extensions.Logging;
using Remba.Model.Entities;
using Remba.Repository.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public class EntityLookupService : IEntityLookupService
{
    private readonly IEnumerable<IEntityLookupProvider> _providers;
    private readonly IEntityRepository _entityRepository;
    private readonly ILogger<EntityLookupService> _logger;

    public EntityLookupService(
        IEnumerable<IEntityLookupProvider> providers,
        IEntityRepository entityRepository,
        ILogger<EntityLookupService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _entityRepository = entityRepository ?? throw new ArgumentNullException(nameof(entityRepository));
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
        IReadOnlyList<EntitySearchResultDto> providerResults = [];

        if (provider is not null)
        {
            _logger.LogDebug(
                "Searching entities via {Provider} for type {EntityType}",
                provider.ProviderKey,
                normalisedType);

            providerResults = await provider
                .SearchAsync(normalisedType, normalisedQuery, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug(
                "No external provider for type {EntityType}; searching local catalog only",
                normalisedType);
        }

        if (providerResults.Count > 0)
        {
            return providerResults;
        }

        var local = await _entityRepository
            .SearchAsync(normalisedType, normalisedQuery, limit: 10, cancellationToken)
            .ConfigureAwait(false);

        return local
            .Select(MapLocalEntity)
            .Where(result => IsDecentMatch(result, normalisedQuery))
            .Take(10)
            .ToList();
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
        if (provider is not null)
        {
            var remote = await provider
                .GetByIdentifierAsync(
                    normalisedType,
                    normalisedIdentifierType,
                    normalisedIdentifierValue,
                    cancellationToken)
                .ConfigureAwait(false);

            if (remote is not null)
            {
                return remote;
            }
        }

        var local = await _entityRepository
            .FindByIdentifierAsync(normalisedIdentifierType, normalisedIdentifierValue, cancellationToken)
            .ConfigureAwait(false);

        if (local is null
            || (!string.IsNullOrWhiteSpace(local.Type)
                && !string.Equals(local.Type, normalisedType, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return MapLocalEntity(local);
    }

    private static EntitySearchResultDto MapLocalEntity(EntityDbo entity)
    {
        var identifier = entity.Identifiers.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Type) && !string.IsNullOrWhiteSpace(item.Value));

        return new EntitySearchResultDto
        {
            Provider = EntityLookupProviders.Remba,
            EntityId = entity.Id,
            IdentifierType = identifier?.Type ?? string.Empty,
            IdentifierValue = identifier?.Value ?? entity.Id,
            Name = entity.Name,
            Author = GetMetadata(entity, "author"),
            Director = GetMetadata(entity, "director"),
            Actors = GetMetadata(entity, "actors"),
            Genre = GetMetadata(entity, "genre"),
            Themes = GetMetadata(entity, "themes"),
            Year = GetMetadata(entity, "year"),
            ImageUrl = GetMetadata(entity, "imageUrl"),
            Link = entity.Link,
            Subtitle = GetMetadata(entity, "author")
                ?? GetMetadata(entity, "director")
                ?? GetMetadata(entity, "subtitle")
        };
    }

    private static bool IsDecentMatch(EntitySearchResultDto result, string query)
    {
        var normalisedName = (result.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalisedName))
        {
            return false;
        }

        if (normalisedName.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(result.IdentifierValue)
            && result.IdentifierValue.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow close matches where the query contains most of the title (short titles).
        return query.Contains(normalisedName, StringComparison.OrdinalIgnoreCase)
               && normalisedName.Length >= 3;
    }

    private static string? GetMetadata(EntityDbo entity, string key) =>
        entity.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
