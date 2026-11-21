using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Model.System;
using Remba.Repository.System;
using Remba.Utility;

namespace Remba.Service.System;

public class SuggestedListService : ISuggestedListService
{
    private readonly ISuggestedListRepository _repository;
    private readonly ILogger<SuggestedListService> _logger;

    public SuggestedListService(
        ISuggestedListRepository repository,
        ILogger<SuggestedListService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var template in SuggestedListDefaults.CreateAll(now))
        {
            var existing = await _repository
                .GetBySeedKeyAsync(template.SeedKey, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                continue;
            }

            await _repository.CreateAsync(template, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Seeded suggested list '{SeedKey}'", template.SeedKey);
        }
    }

    public async Task<IReadOnlyList<SuggestedListDto>> GetAllAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => includeDisabled || item.Enabled)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<SuggestedListDto?> GetBySeedKeyAsync(
        string seedKey,
        CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseSeedKey(seedKey);
        if (normalised is null)
        {
            return null;
        }

        var item = await _repository.GetBySeedKeyAsync(normalised, cancellationToken).ConfigureAwait(false);
        return item is null ? null : MapToDto(item);
    }

    public async Task<SuggestedListDto> UpdateAsync(
        string seedKey,
        UpdateSuggestedListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalised = NormaliseSeedKey(seedKey)
            ?? throw new ArgumentException("Seed key is required.", nameof(seedKey));

        var existing = await _repository.GetBySeedKeyAsync(normalised, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Suggested list '{normalised}' was not found.");

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Suggested list name cannot be empty.");
            }

            existing.Name = name;
        }

        if (request.Description is not null)
        {
            existing.Description = StringUtilities.NormalizeWhitespace(request.Description);
        }

        if (request.SortOrder is not null)
        {
            existing.SortOrder = request.SortOrder.Value;
        }

        if (request.Enabled is not null)
        {
            existing.Enabled = request.Enabled.Value;
        }

        if (request.EntityType is not null)
        {
            var entityType = StringUtilities.NormalizeWhitespace(request.EntityType);
            existing.EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType;
        }

        if (request.Identifier is not null)
        {
            existing.Identifier = NormaliseIdentifier(request.Identifier);
        }

        if (request.Enrichment is not null)
        {
            existing.Enrichment = NormaliseEnrichment(request.Enrichment);
        }

        if (request.DefaultEntryAttributes is not null)
        {
            existing.DefaultEntryAttributes = NormaliseAttributes(request.DefaultEntryAttributes);
        }

        existing.Edited = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated suggested list '{SeedKey}'", updated.SeedKey);
        return MapToDto(updated);
    }

    private static string? NormaliseSeedKey(string? seedKey)
    {
        if (string.IsNullOrWhiteSpace(seedKey))
        {
            return null;
        }

        return seedKey.Trim().ToLowerInvariant();
    }

    private static SuggestedListIdentifierDbo NormaliseIdentifier(SuggestedListIdentifierDbo identifier)
    {
        var type = StringUtilities.NormalizeWhitespace(identifier.Type).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Identifier type is required.");
        }

        var label = StringUtilities.NormalizeWhitespace(identifier.Label);
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Identifier label is required.");
        }

        var urlTemplate = StringUtilities.NormalizeWhitespace(identifier.UrlTemplate);
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            throw new ArgumentException("Identifier URL template is required.");
        }

        return new SuggestedListIdentifierDbo
        {
            Type = type,
            Label = label,
            UrlTemplate = urlTemplate
        };
    }

    private static SuggestedListEnrichmentDbo NormaliseEnrichment(SuggestedListEnrichmentDbo enrichment)
    {
        var provider = StringUtilities.NormalizeWhitespace(enrichment.Provider).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Enrichment provider is required.");
        }

        var available = enrichment.AvailableAttributes
            .Select(attribute =>
            {
                var key = StringUtilities.NormalizeWhitespace(attribute.Key);
                var name = StringUtilities.NormalizeWhitespace(attribute.Name);
                var type = StringUtilities.NormalizeWhitespace(attribute.Type);

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Enrichment attributes require key and name.");
                }

                return new SuggestedListEnrichmentAttributeDbo
                {
                    Key = key,
                    Name = name,
                    Type = string.IsNullOrWhiteSpace(type) ? "text" : type.ToLowerInvariant()
                };
            })
            .ToList();

        var selected = enrichment.DefaultSelectedAttributeKeys
            .Select(key => StringUtilities.NormalizeWhitespace(key))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var availableKeys = available.Select(attribute => attribute.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in selected)
        {
            if (!availableKeys.Contains(key))
            {
                throw new ArgumentException($"Default selected attribute '{key}' is not in available attributes.");
            }
        }

        return new SuggestedListEnrichmentDbo
        {
            Provider = provider,
            AvailableAttributes = available,
            DefaultSelectedAttributeKeys = selected
        };
    }

    private static List<ListAttributeDefinitionDbo> NormaliseAttributes(
        IEnumerable<ListAttributeDefinitionDbo> attributes) =>
        attributes
            .Select(attribute =>
            {
                var key = StringUtilities.NormalizeWhitespace(attribute.Key);
                var name = StringUtilities.NormalizeWhitespace(attribute.Name);
                var type = StringUtilities.NormalizeWhitespace(attribute.Type);
                var scope = StringUtilities.NormalizeWhitespace(attribute.Scope);

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Entry attributes require key and name.");
                }

                return new ListAttributeDefinitionDbo
                {
                    Key = key,
                    Name = name,
                    Description = StringUtilities.NormalizeWhitespace(attribute.Description),
                    Type = string.IsNullOrWhiteSpace(type) ? "text" : type.ToLowerInvariant(),
                    Scope = string.IsNullOrWhiteSpace(scope) ? "entry" : scope.ToLowerInvariant()
                };
            })
            .ToList();

    private static SuggestedListDto MapToDto(SuggestedListDbo source) =>
        new()
        {
            SeedKey = source.SeedKey,
            Name = source.Name,
            Description = source.Description,
            SortOrder = source.SortOrder,
            Enabled = source.Enabled,
            EntityType = source.EntityType,
            Identifier = new SuggestedListIdentifierDto
            {
                Type = source.Identifier.Type,
                Label = source.Identifier.Label,
                UrlTemplate = source.Identifier.UrlTemplate
            },
            Enrichment = new SuggestedListEnrichmentDto
            {
                Provider = source.Enrichment.Provider,
                AvailableAttributes = source.Enrichment.AvailableAttributes
                    .Select(attribute => new SuggestedListEnrichmentAttributeDto
                    {
                        Key = attribute.Key,
                        Name = attribute.Name,
                        Type = attribute.Type
                    })
                    .ToList(),
                DefaultSelectedAttributeKeys = [.. source.Enrichment.DefaultSelectedAttributeKeys]
            },
            DefaultEntryAttributes = source.DefaultEntryAttributes
                .Select(attribute => new ListAttributeDefinitionDto
                {
                    Key = attribute.Key,
                    Name = attribute.Name,
                    Description = attribute.Description,
                    Type = attribute.Type,
                    Scope = attribute.Scope
                })
                .ToList(),
            Added = source.Added,
            Edited = source.Edited
        };
}
