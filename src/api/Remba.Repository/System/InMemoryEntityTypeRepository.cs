using System.Collections.Concurrent;
using System.Text.Json;
using Remba.Model.System;
using Remba.Utility;

namespace Remba.Repository.System;

public class InMemoryEntityTypeRepository : IEntityTypeRepository
{
    private readonly ConcurrentDictionary<string, EntityTypeDbo> _items = new(StringComparer.Ordinal);

    public Task<EntityTypeDbo?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(id)
            || !_items.TryGetValue(id, out var item)
            || item.RecordType != SystemRecordTypes.EntityType)
        {
            return Task.FromResult<EntityTypeDbo?>(null);
        }

        return Task.FromResult(Clone(item));
    }

    public Task<EntityTypeDbo?> FindByKeyAsync(
        string key,
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        var normalised = key.Trim().ToLowerInvariant();
        var match = _items.Values.FirstOrDefault(item =>
            item.RecordType == SystemRecordTypes.EntityType
            && string.Equals(item.Key, normalised, StringComparison.OrdinalIgnoreCase)
            && IsVisibleTo(item, ownerId));

        return Task.FromResult(Clone(match));
    }

    public Task<IReadOnlyList<EntityTypeDbo>> GetAvailableAsync(
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        var results = _items.Values
            .Where(item => item.RecordType == SystemRecordTypes.EntityType && IsVisibleTo(item, ownerId))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => Clone(item)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<EntityTypeDbo>>(results);
    }

    public Task<EntityTypeDbo> CreateAsync(EntityTypeDbo entityType, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entityType.Id))
        {
            throw new ArgumentException("Entity type id is required.", nameof(entityType));
        }

        if (!_items.TryAdd(entityType.Id, Clone(entityType)!))
        {
            throw new InvalidOperationException($"Entity type '{entityType.Id}' already exists.");
        }

        return Task.FromResult(Clone(entityType)!);
    }

    private static bool IsVisibleTo(EntityTypeDbo item, string? ownerId) =>
        item.Visibility is EntityTypeVisibility.System or EntityTypeVisibility.Public
        || (item.Visibility == EntityTypeVisibility.Private
            && !string.IsNullOrWhiteSpace(ownerId)
            && string.Equals(item.OwnerId, ownerId, StringComparison.Ordinal));

    private static EntityTypeDbo? Clone(EntityTypeDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<EntityTypeDbo>(JsonSerializer.Serialize(source));
    }
}
