using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Remba.Model.System;
using Remba.Repository.System;
using Remba.Utility;

namespace Remba.Service.System;

public partial class EntityTypeService : IEntityTypeService
{
    private readonly IEntityTypeRepository _repository;
    private readonly ILogger<EntityTypeService> _logger;

    public EntityTypeService(
        IEntityTypeRepository repository,
        ILogger<EntityTypeService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var template in EntityTypeDefaults.CreateAll(now))
        {
            var existing = await _repository.GetByIdAsync(template.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                continue;
            }

            await _repository.CreateAsync(template, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Seeded entity type '{Key}'", template.Key);
        }
    }

    public async Task<IReadOnlyList<EntityTypeDto>> GetAvailableAsync(
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAvailableAsync(ownerId, cancellationToken).ConfigureAwait(false);

        return items
            .OrderBy(item => item.Visibility == EntityTypeVisibility.System ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<EntityTypeDto> CreatePrivateAsync(
        string ownerId,
        CreateEntityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (StringUtilities.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var name = StringUtilities.NormalizeWhitespace(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Entity type name is required.");
        }

        var key = CreateKey(name);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Entity type name must contain letters or numbers.");
        }

        var existing = await _repository.FindByKeyAsync(key, ownerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Visibility == EntityTypeVisibility.System
                || existing.Visibility == EntityTypeVisibility.Public
                || string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Entity type '{existing.Name}' already exists.");
            }
        }

        var created = await _repository
            .CreateAsync(
                new EntityTypeDbo
                {
                    Id = $"entity-type:{ownerId}:{key}",
                    RecordType = SystemRecordTypes.EntityType,
                    Key = key,
                    Name = name,
                    Visibility = EntityTypeVisibility.Private,
                    OwnerId = ownerId,
                    SupportsLookup = false,
                    Added = DateTime.UtcNow,
                    Edited = null
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Created private entity type '{Key}' for owner {OwnerId}", created.Key, ownerId);
        return MapToDto(created);
    }

    private static string CreateKey(string name)
    {
        var slug = NonKeyChars().Replace(name.Trim().ToLowerInvariant(), "-");
        slug = CollapseHyphens().Replace(slug, "-").Trim('-');
        if (slug.Length > 40)
        {
            slug = slug[..40].Trim('-');
        }

        return slug;
    }

    private static EntityTypeDto MapToDto(EntityTypeDbo source) =>
        new()
        {
            Key = source.Key,
            Name = source.Name,
            Visibility = source.Visibility,
            SupportsLookup = source.SupportsLookup,
            Added = source.Added,
            Edited = source.Edited
        };

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonKeyChars();

    [GeneratedRegex(@"-+", RegexOptions.CultureInvariant)]
    private static partial Regex CollapseHyphens();
}
