using System.Collections.Concurrent;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class InMemoryListInviteRepository : IListInviteRepository
{
    private readonly ConcurrentDictionary<string, ListInviteDbo> _invites = new(StringComparer.Ordinal);

    public Task<ListInviteDbo?> GetByIdAsync(
        string listOwnerId,
        string inviteId,
        CancellationToken cancellationToken = default)
    {
        if (!_invites.TryGetValue(inviteId, out var invite)
            || !string.Equals(invite.OwnerId, listOwnerId, StringComparison.Ordinal)
            || invite.RecordType != ListRecordTypes.ListInvite)
        {
            return Task.FromResult<ListInviteDbo?>(null);
        }

        return Task.FromResult(Clone(invite));
    }

    public Task<ListInviteDbo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalised = code.Trim().ToUpperInvariant();
        var match = _invites.Values.FirstOrDefault(invite =>
            invite.RecordType == ListRecordTypes.ListInvite
            && string.Equals(invite.Code, normalised, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(Clone(match));
    }

    public Task<IReadOnlyList<ListInviteDbo>> GetByListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var results = _invites.Values
            .Where(invite =>
                string.Equals(invite.OwnerId, listOwnerId, StringComparison.Ordinal)
                && string.Equals(invite.ListId, listId, StringComparison.Ordinal)
                && invite.RecordType == ListRecordTypes.ListInvite)
            .OrderByDescending(invite => invite.Added)
            .Select(invite => Clone(invite)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<ListInviteDbo>>(results);
    }

    public Task<ListInviteDbo> CreateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(invite.Id) || StringUtilities.IsNullOrWhiteSpace(invite.OwnerId))
        {
            throw new ArgumentException("Invite id and owner id are required.", nameof(invite));
        }

        if (!_invites.TryAdd(invite.Id, Clone(invite)!))
        {
            throw new InvalidOperationException($"Invite '{invite.Id}' already exists.");
        }

        return Task.FromResult(Clone(invite)!);
    }

    public Task<ListInviteDbo> UpdateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default)
    {
        if (!_invites.ContainsKey(invite.Id)
            || !string.Equals(_invites[invite.Id].OwnerId, invite.OwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invite '{invite.Id}' was not found.");
        }

        _invites[invite.Id] = Clone(invite)!;
        return Task.FromResult(Clone(invite)!);
    }

    public Task DeleteAsync(string listOwnerId, string inviteId, CancellationToken cancellationToken = default)
    {
        if (_invites.TryGetValue(inviteId, out var invite)
            && string.Equals(invite.OwnerId, listOwnerId, StringComparison.Ordinal))
        {
            _invites.TryRemove(inviteId, out _);
        }

        return Task.CompletedTask;
    }

    private static ListInviteDbo? Clone(ListInviteDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ListInviteDbo
        {
            Id = source.Id,
            RecordType = source.RecordType,
            OwnerId = source.OwnerId,
            ListId = source.ListId,
            ListOwnerId = source.ListOwnerId,
            ListName = source.ListName,
            Code = source.Code,
            Role = source.Role,
            Added = source.Added,
            AddedByUserId = source.AddedByUserId,
            UsedAt = source.UsedAt,
            UsedByUserId = source.UsedByUserId,
            UsedByUsername = source.UsedByUsername,
            UsedByDisplayName = source.UsedByDisplayName
        };
    }
}
