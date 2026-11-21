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

    public Task<IReadOnlyList<EntityDbo>> SearchAsync(
        string entityType,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalisedType = StringUtilities.NormalizeWhitespace(entityType);
        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedType)
            || string.IsNullOrWhiteSpace(normalisedQuery)
            || limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<EntityDbo>>([]);
        }

        var matches = _entities.Values
            .Where(entity =>
                string.Equals(entity.Type, normalisedType, StringComparison.OrdinalIgnoreCase)
                && (
                    (entity.Name ?? string.Empty).Contains(normalisedQuery, StringComparison.OrdinalIgnoreCase)
                    || entity.Identifiers.Any(identifier =>
                        (identifier.Value ?? string.Empty).Contains(normalisedQuery, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(entity =>
                string.Equals(entity.Name, normalisedQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entity =>
                (entity.Name ?? string.Empty).StartsWith(normalisedQuery, StringComparison.OrdinalIgnoreCase))
            .ThenBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 25))
            .Select(Clone)
            .Where(entity => entity is not null)
            .Cast<EntityDbo>()
            .ToList();

        return Task.FromResult<IReadOnlyList<EntityDbo>>(matches);
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
