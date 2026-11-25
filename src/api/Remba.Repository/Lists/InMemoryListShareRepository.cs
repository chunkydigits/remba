using System.Collections.Concurrent;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class InMemoryListShareRepository : IListShareRepository
{
    private readonly ConcurrentDictionary<string, ListShareDbo> _shares = new(StringComparer.Ordinal);

    private static string Key(string partitionOwnerId, string shareId) => $"{partitionOwnerId}:{shareId}";

    public Task<ListShareDbo?> GetAsync(
        string partitionOwnerId,
        string shareId,
        CancellationToken cancellationToken = default)
    {
        if (!_shares.TryGetValue(Key(partitionOwnerId, shareId), out var share)
            || share.RecordType != ListRecordTypes.ListShare)
        {
            return Task.FromResult<ListShareDbo?>(null);
        }

        return Task.FromResult(Clone(share));
    }

    public Task<ListShareDbo?> GetForMemberAsync(
        string memberUserId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var match = _shares.Values.FirstOrDefault(share =>
            string.Equals(share.OwnerId, memberUserId, StringComparison.Ordinal)
            && string.Equals(share.MemberUserId, memberUserId, StringComparison.Ordinal)
            && string.Equals(share.ListId, listId, StringComparison.Ordinal)
            && share.RecordType == ListRecordTypes.ListShare);

        return Task.FromResult(Clone(match));
    }

    public Task<IReadOnlyList<ListShareDbo>> GetByListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var results = _shares.Values
            .Where(share =>
                string.Equals(share.OwnerId, listOwnerId, StringComparison.Ordinal)
                && string.Equals(share.ListId, listId, StringComparison.Ordinal)
                && share.RecordType == ListRecordTypes.ListShare)
            .OrderBy(share => share.Added)
            .Select(share => Clone(share)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<ListShareDbo>>(results);
    }

    public Task<IReadOnlyList<ListShareDbo>> GetByMemberAsync(
        string memberUserId,
        CancellationToken cancellationToken = default)
    {
        var results = _shares.Values
            .Where(share =>
                string.Equals(share.OwnerId, memberUserId, StringComparison.Ordinal)
                && string.Equals(share.MemberUserId, memberUserId, StringComparison.Ordinal)
                && share.RecordType == ListRecordTypes.ListShare)
            .OrderByDescending(share => share.Added)
            .Select(share => Clone(share)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<ListShareDbo>>(results);
    }

    public Task UpsertAsync(ListShareDbo share, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(share.Id) || StringUtilities.IsNullOrWhiteSpace(share.OwnerId))
        {
            throw new ArgumentException("Share id and owner id are required.", nameof(share));
        }

        _shares[Key(share.OwnerId, share.Id)] = Clone(share)!;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string partitionOwnerId,
        string shareId,
        CancellationToken cancellationToken = default)
    {
        _shares.TryRemove(Key(partitionOwnerId, shareId), out _);
        return Task.CompletedTask;
    }

    private static ListShareDbo? Clone(ListShareDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ListShareDbo
        {
            Id = source.Id,
            RecordType = source.RecordType,
            OwnerId = source.OwnerId,
            ListId = source.ListId,
            ListOwnerId = source.ListOwnerId,
            ListName = source.ListName,
            MemberUserId = source.MemberUserId,
            MemberUsername = source.MemberUsername,
            MemberDisplayName = source.MemberDisplayName,
            Role = source.Role,
            ShareLabel = source.ShareLabel,
            Added = source.Added,
            AddedByUserId = source.AddedByUserId
        };
    }
}
