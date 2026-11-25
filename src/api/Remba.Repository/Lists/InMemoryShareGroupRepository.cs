using System.Collections.Concurrent;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class InMemoryShareGroupRepository : IShareGroupRepository
{
    private readonly ConcurrentDictionary<string, ShareGroupDbo> _groups = new(StringComparer.Ordinal);

    public Task<ShareGroupDbo?> GetByIdAsync(
        string ownerId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        if (!_groups.TryGetValue(groupId, out var group)
            || !string.Equals(group.OwnerId, ownerId, StringComparison.Ordinal)
            || group.RecordType != ListRecordTypes.ShareGroup)
        {
            return Task.FromResult<ShareGroupDbo?>(null);
        }

        return Task.FromResult(Clone(group));
    }

    public Task<IReadOnlyList<ShareGroupDbo>> GetByOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var results = _groups.Values
            .Where(group =>
                string.Equals(group.OwnerId, ownerId, StringComparison.Ordinal)
                && group.RecordType == ListRecordTypes.ShareGroup)
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => Clone(group)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<ShareGroupDbo>>(results);
    }

    public Task<ShareGroupDbo> CreateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(group.Id) || StringUtilities.IsNullOrWhiteSpace(group.OwnerId))
        {
            throw new ArgumentException("Group id and owner id are required.", nameof(group));
        }

        if (!_groups.TryAdd(group.Id, Clone(group)!))
        {
            throw new InvalidOperationException($"Share group '{group.Id}' already exists.");
        }

        return Task.FromResult(Clone(group)!);
    }

    public Task<ShareGroupDbo> UpdateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default)
    {
        if (!_groups.ContainsKey(group.Id)
            || !string.Equals(_groups[group.Id].OwnerId, group.OwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Share group '{group.Id}' was not found.");
        }

        _groups[group.Id] = Clone(group)!;
        return Task.FromResult(Clone(group)!);
    }

    public Task DeleteAsync(string ownerId, string groupId, CancellationToken cancellationToken = default)
    {
        if (_groups.TryGetValue(groupId, out var group)
            && string.Equals(group.OwnerId, ownerId, StringComparison.Ordinal))
        {
            _groups.TryRemove(groupId, out _);
        }

        return Task.CompletedTask;
    }

    private static ShareGroupDbo? Clone(ShareGroupDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ShareGroupDbo
        {
            Id = source.Id,
            RecordType = source.RecordType,
            OwnerId = source.OwnerId,
            Name = source.Name,
            Members = source.Members
                .Select(member => new ShareGroupMemberDbo
                {
                    UserId = member.UserId,
                    Username = member.Username,
                    DisplayName = member.DisplayName
                })
                .ToList(),
            Added = source.Added,
            Edited = source.Edited,
            AddedByUserId = source.AddedByUserId,
            ChangedByUserId = source.ChangedByUserId
        };
    }
}