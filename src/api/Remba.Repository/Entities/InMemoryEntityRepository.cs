using System.Collections.Concurrent;
using System.Text.Json;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Repository.Entities;

public class InMemoryEntityRepository : IEntityRepository
{
    private readonly ConcurrentDictionary<string, EntityDbo> _entities = new(StringComparer.Ordinal);

    public Task<EntityDbo?> GetByIdAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entityId) || !_entities.TryGetValue(entityId, out var entity))
        {
            return Task.FromResult<EntityDbo?>(null);
        }

        return Task.FromResult(Clone(entity));
    }

    public Task<EntityDbo?> FindByIdentifierAsync(
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(identifierType)
            || StringUtilities.IsNullOrWhiteSpace(identifierValue))
        {
            return Task.FromResult<EntityDbo?>(null);
        }

        var match = _entities.Values.FirstOrDefault(entity =>
            entity.Identifiers.Any(identifier =>
                string.Equals(identifier.Type, identifierType.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(identifier.Value, identifierValue.Trim(), StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(Clone(match));
    }

    public Task<EntityDbo> CreateAsync(EntityDbo entity, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentException("Entity id is required.", nameof(entity));
        }

        if (!_entities.TryAdd(entity.Id, Clone(entity)!))
        {
            throw new InvalidOperationException($"Entity '{entity.Id}' already exists.");
        }

        return Task.FromResult(Clone(entity)!);
    }

    public Task<EntityDbo> UpdateAsync(EntityDbo entity, CancellationToken cancellationToken = default)
    {
        if (!_entities.ContainsKey(entity.Id))
        {
            throw new InvalidOperationException($"Entity '{entity.Id}' was not found.");
        }

        _entities[entity.Id] = Clone(entity)!;
        return Task.FromResult(Clone(entity)!);
    }

    private static EntityDbo? Clone(EntityDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<EntityDbo>(JsonSerializer.Serialize(source));
    }
}
