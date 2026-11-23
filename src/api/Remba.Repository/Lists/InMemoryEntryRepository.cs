using System.Collections.Concurrent;
using System.Text.Json;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class InMemoryEntryRepository : IEntryRepository
{
    private readonly ConcurrentDictionary<string, EntryDbo> _entries = new(StringComparer.Ordinal);

    public Task<EntryDbo?> GetByIdAsync(string ownerId, string entryId, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(entryId, out var entry)
            || !string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
            || entry.RecordType != ListRecordTypes.Entry)
        {
            return Task.FromResult<EntryDbo?>(null);
        }

        return Task.FromResult(Clone(entry));
    }

    public Task<IReadOnlyList<EntryDbo>> GetByListAsync(
        string ownerId,
        string listId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var results = _entries.Values
            .Where(entry =>
                string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
                && string.Equals(entry.ListId, listId, StringComparison.Ordinal)
                && entry.RecordType == ListRecordTypes.Entry
                && (includeDeleted || entry.Deleted is null))
            .OrderBy(entry => entry.Order)
            .Select(entry => Clone(entry)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<EntryDbo>>(results);
    }

    public Task<EntryDbo?> FindActiveByEntityAsync(
        string ownerId,
        string listId,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var match = _entries.Values.FirstOrDefault(entry =>
            string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
            && string.Equals(entry.ListId, listId, StringComparison.Ordinal)
            && string.Equals(entry.EntityId, entityId, StringComparison.Ordinal)
            && entry.RecordType == ListRecordTypes.Entry
            && entry.Deleted is null);

        return Task.FromResult(Clone(match));
    }

    public Task<IReadOnlyList<string>> FindActiveListIdsContainingAsync(
        string ownerId,
        string? identifierType,
        string? identifierValue,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        var hasIdentifier = !string.IsNullOrWhiteSpace(identifierType) && !string.IsNullOrWhiteSpace(identifierValue);
        var hasEntityId = !string.IsNullOrWhiteSpace(entityId);
        if (StringUtilities.IsNullOrWhiteSpace(ownerId) || (!hasIdentifier && !hasEntityId))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var listIds = _entries.Values
            .Where(entry =>
                string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
                && entry.RecordType == ListRecordTypes.Entry
                && entry.Deleted is null
                && (
                    (hasEntityId && string.Equals(entry.EntityId, entityId, StringComparison.Ordinal))
                    || (hasIdentifier
                        && string.Equals(entry.IdentifierType, identifierType, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(entry.IdentifierValue, identifierValue, StringComparison.OrdinalIgnoreCase))
                ))
            .Select(entry => entry.ListId)
            .Where(listId => !string.IsNullOrWhiteSpace(listId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(listIds);
    }

    public Task<int> GetNextOrderAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        var max = _entries.Values
            .Where(entry =>
                string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
                && string.Equals(entry.ListId, listId, StringComparison.Ordinal)
                && entry.RecordType == ListRecordTypes.Entry)
            .Select(entry => entry.Order)
            .DefaultIfEmpty(0)
            .Max();

        return Task.FromResult(max + 1);
    }

    public Task<EntryDbo> CreateAsync(EntryDbo entry, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entry.Id) || StringUtilities.IsNullOrWhiteSpace(entry.OwnerId))
        {
            throw new ArgumentException("Entry id and owner id are required.", nameof(entry));
        }

        if (!_entries.TryAdd(entry.Id, Clone(entry)!))
        {
            throw new InvalidOperationException($"Entry '{entry.Id}' already exists.");
        }

        return Task.FromResult(Clone(entry)!);
    }

    public Task<EntryDbo> UpdateAsync(EntryDbo entry, CancellationToken cancellationToken = default)
    {
        if (!_entries.ContainsKey(entry.Id)
            || !string.Equals(_entries[entry.Id].OwnerId, entry.OwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Entry '{entry.Id}' was not found.");
        }

        _entries[entry.Id] = Clone(entry)!;
        return Task.FromResult(Clone(entry)!);
    }

    private static EntryDbo? Clone(EntryDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<EntryDbo>(JsonSerializer.Serialize(source));
    }
}
