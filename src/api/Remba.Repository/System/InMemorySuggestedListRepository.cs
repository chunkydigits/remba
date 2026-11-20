using System.Collections.Concurrent;
using System.Text.Json;
using Remba.Model.System;
using Remba.Utility;

namespace Remba.Repository.System;

public class InMemorySuggestedListRepository : ISuggestedListRepository
{
    private readonly ConcurrentDictionary<string, SuggestedListDbo> _items = new(StringComparer.Ordinal);

    public Task<SuggestedListDbo?> GetBySeedKeyAsync(string seedKey, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(seedKey)
            || !_items.TryGetValue(seedKey, out var item)
            || item.RecordType != SystemRecordTypes.SuggestedList)
        {
            return Task.FromResult<SuggestedListDbo?>(null);
        }

        return Task.FromResult(Clone(item));
    }

    public Task<IReadOnlyList<SuggestedListDbo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = _items.Values
            .Where(item => item.RecordType == SystemRecordTypes.SuggestedList)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => Clone(item)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<SuggestedListDbo>>(results);
    }

    public Task<SuggestedListDbo> CreateAsync(SuggestedListDbo suggestedList, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(suggestedList.Id)
            || StringUtilities.IsNullOrWhiteSpace(suggestedList.SeedKey))
        {
            throw new ArgumentException("Suggested list id and seed key are required.", nameof(suggestedList));
        }

        if (!_items.TryAdd(suggestedList.Id, Clone(suggestedList)!))
        {
            throw new InvalidOperationException($"Suggested list '{suggestedList.Id}' already exists.");
        }

        return Task.FromResult(Clone(suggestedList)!);
    }

    public Task<SuggestedListDbo> UpdateAsync(SuggestedListDbo suggestedList, CancellationToken cancellationToken = default)
    {
        if (!_items.ContainsKey(suggestedList.Id))
        {
            throw new InvalidOperationException($"Suggested list '{suggestedList.Id}' was not found.");
        }

        _items[suggestedList.Id] = Clone(suggestedList)!;
        return Task.FromResult(Clone(suggestedList)!);
    }

    private static SuggestedListDbo? Clone(SuggestedListDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SuggestedListDbo>(JsonSerializer.Serialize(source));
    }
}
