using System.Collections.Concurrent;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class InMemoryListRepository : IListRepository
{
    private readonly ConcurrentDictionary<string, ListDbo> _lists = new(StringComparer.Ordinal);

    public Task<ListDbo?> GetByIdAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        if (!_lists.TryGetValue(listId, out var list)
            || !string.Equals(list.OwnerId, ownerId, StringComparison.Ordinal)
            || list.RecordType != ListRecordTypes.List)
        {
            return Task.FromResult<ListDbo?>(null);
        }

        return Task.FromResult(Clone(list));
    }

    public Task<IReadOnlyList<ListDbo>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var results = _lists.Values
            .Where(list =>
                string.Equals(list.OwnerId, ownerId, StringComparison.Ordinal)
                && list.RecordType == ListRecordTypes.List)
            .OrderByDescending(list => list.Added)
            .Select(list => Clone(list)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<ListDbo>>(results);
    }

    public Task<ListDbo> CreateAsync(ListDbo list, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(list.Id) || StringUtilities.IsNullOrWhiteSpace(list.OwnerId))
        {
            throw new ArgumentException("List id and owner id are required.", nameof(list));
        }

        if (!_lists.TryAdd(list.Id, Clone(list)!))
        {
            throw new InvalidOperationException($"List '{list.Id}' already exists.");
        }

        return Task.FromResult(Clone(list)!);
    }

    public Task<ListDbo> UpdateAsync(ListDbo list, CancellationToken cancellationToken = default)
    {
        if (!_lists.ContainsKey(list.Id)
            || !string.Equals(_lists[list.Id].OwnerId, list.OwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"List '{list.Id}' was not found.");
        }

        _lists[list.Id] = Clone(list)!;
        return Task.FromResult(Clone(list)!);
    }

    public Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        if (_lists.TryGetValue(listId, out var list)
            && string.Equals(list.OwnerId, ownerId, StringComparison.Ordinal))
        {
            _lists.TryRemove(listId, out _);
        }

        return Task.CompletedTask;
    }

    private static ListDbo? Clone(ListDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ListDbo
        {
            Id = source.Id,
            RecordType = source.RecordType,
            OwnerId = source.OwnerId,
            Visibility = source.Visibility,
            Name = source.Name,
            Description = source.Description,
            ReferralLink = source.ReferralLink,
            Configuration = new ListConfigurationDbo
            {
                EntityType = source.Configuration?.EntityType,
                Attributes = (source.Configuration?.Attributes ?? [])
                    .Select(attribute => new ListAttributeDefinitionDbo
                    {
                        Key = attribute.Key,
                        Name = attribute.Name,
                        Description = attribute.Description,
                        Type = attribute.Type,
                        Scope = attribute.Scope
                    })
                    .ToList()
            },
            ViewSettings = new ListViewSettingsDbo
            {
                SortBy = source.ViewSettings.SortBy,
                SortDirection = source.ViewSettings.SortDirection,
                ViewMode = source.ViewSettings.ViewMode,
                VisibleFields = [.. source.ViewSettings.VisibleFields]
            },
            ExternalLinks = source.ExternalLinks
                .Select(link => new ExternalLinkDbo
                {
                    Url = link.Url,
                    Title = link.Title,
                    Platform = link.Platform
                })
                .ToList(),
            Added = source.Added,
            AddedByUserId = source.AddedByUserId,
            Edited = source.Edited,
            ChangedByUserId = source.ChangedByUserId
        };
    }
}
