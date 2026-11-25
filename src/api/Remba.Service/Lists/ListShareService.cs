using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Model.Users;
using Remba.Repository.Lists;
using Remba.Service.Users;
using Remba.Utility;

namespace Remba.Service.Lists;

public class ListShareService : IListShareService
{
    private readonly IListRepository _listRepository;
    private readonly IListShareRepository _listShareRepository;
    private readonly IShareGroupRepository _shareGroupRepository;
    private readonly IUserService _userService;
    private readonly ILogger<ListShareService> _logger;

    public ListShareService(
        IListRepository listRepository,
        IListShareRepository listShareRepository,
        IShareGroupRepository shareGroupRepository,
        IUserService userService,
        ILogger<ListShareService> logger)
    {
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
        _listShareRepository = listShareRepository
                               ?? throw new ArgumentNullException(nameof(listShareRepository));
        _shareGroupRepository = shareGroupRepository
                                ?? throw new ArgumentNullException(nameof(shareGroupRepository));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ListAccessContext?> TryResolveAccessAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(userId) || StringUtilities.IsNullOrWhiteSpace(listId))
        {
            return null;
        }

        var owned = await _listRepository.GetByIdAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (owned is not null)
        {
            return new ListAccessContext { List = owned, Role = ListShareRoles.Owner };
        }

        var share = await _listShareRepository
            .GetForMemberAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (share is null)
        {
            return null;
        }

        var list = await _listRepository
            .GetByIdAsync(share.ListOwnerId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (list is null)
        {
            return null;
        }

        return new ListAccessContext { List = list, Role = share.Role };
    }

    public async Task<ListAccessContext> RequireAccessAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var access = await TryResolveAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (access is null)
        {
            throw new KeyNotFoundException("List was not found.");
        }

        return access;
    }

    public async Task<IReadOnlyList<ListShareMemberDto>> GetMembersAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role) && !access.IsOwner)
        {
            // Members can see who else is on a shared list.
        }

        var shares = await _listShareRepository
            .GetByListAsync(access.List.OwnerId, listId, cancellationToken)
            .ConfigureAwait(false);

        return shares.Select(MapMember).ToList();
    }

    public async Task<IReadOnlyList<ListShareMemberDto>> ShareAsync(
        string userId,
        string listId,
        ShareListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await RequireAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can manage sharing.");
        }

        var role = NormaliseRole(request.Role);
        var shareLabel = NormaliseOptional(request.ShareLabel);
        var targets = new Dictionary<string, UserDto>(StringComparer.Ordinal);

        foreach (var username in request.Usernames ?? [])
        {
            var user = await ResolveUsernameAsync(username, cancellationToken).ConfigureAwait(false);
            if (string.Equals(user.Id, userId, StringComparison.Ordinal))
            {
                throw new ArgumentException("You cannot share a list with yourself.");
            }

            targets[user.Id] = user;
        }

        if (!string.IsNullOrWhiteSpace(request.ShareGroupId))
        {
            var group = await _shareGroupRepository
                .GetByIdAsync(userId, request.ShareGroupId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Share group was not found.");

            shareLabel ??= group.Name;

            foreach (var member in group.Members)
            {
                if (string.Equals(member.UserId, userId, StringComparison.Ordinal))
                {
                    continue;
                }

                var user = await _userService.GetByIdAsync(member.UserId, cancellationToken).ConfigureAwait(false);
                if (user is null)
                {
                    continue;
                }

                targets[user.Id] = user;
            }
        }

        if (targets.Count == 0)
        {
            throw new ArgumentException("Provide at least one username or a share group with members.");
        }

        var now = DateTime.UtcNow;
        var list = access.List;

        foreach (var target in targets.Values)
        {
            var shareId = BuildShareId(listId, target.Id);
            var baseShare = new ListShareDbo
            {
                Id = shareId,
                RecordType = ListRecordTypes.ListShare,
                ListId = list.Id,
                ListOwnerId = list.OwnerId,
                ListName = list.Name,
                MemberUserId = target.Id,
                MemberUsername = target.Username,
                MemberDisplayName = target.DisplayName,
                Role = role,
                ShareLabel = shareLabel,
                Added = now,
                AddedByUserId = userId
            };

            var ownerCopy = CloneShare(baseShare, list.OwnerId);
            var memberCopy = CloneShare(baseShare, target.Id);

            await _listShareRepository.UpsertAsync(ownerCopy, cancellationToken).ConfigureAwait(false);
            await _listShareRepository.UpsertAsync(memberCopy, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Shared list {ListId} with {MemberCount} member(s) by {UserId}",
            listId,
            targets.Count,
            userId);

        return await GetMembersAsync(userId, listId, cancellationToken).ConfigureAwait(false);
    }

    public async Task GrantShareAsync(
        ListDbo list,
        UserDto member,
        string role,
        string grantedByUserId,
        string? shareLabel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        if (string.Equals(list.OwnerId, member.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("You already own this list.");
        }

        var normalisedRole = NormaliseRole(role);
        var now = DateTime.UtcNow;
        var shareId = BuildShareId(list.Id, member.Id);
        var baseShare = new ListShareDbo
        {
            Id = shareId,
            RecordType = ListRecordTypes.ListShare,
            ListId = list.Id,
            ListOwnerId = list.OwnerId,
            ListName = list.Name,
            MemberUserId = member.Id,
            MemberUsername = member.Username,
            MemberDisplayName = member.DisplayName,
            Role = normalisedRole,
            ShareLabel = NormaliseOptional(shareLabel),
            Added = now,
            AddedByUserId = grantedByUserId
        };

        await _listShareRepository.UpsertAsync(CloneShare(baseShare, list.OwnerId), cancellationToken)
            .ConfigureAwait(false);
        await _listShareRepository.UpsertAsync(CloneShare(baseShare, member.Id), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UnshareAsync(
        string userId,
        string listId,
        string memberUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can manage sharing.");
        }

        if (StringUtilities.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException("Member user id is required.", nameof(memberUserId));
        }

        await DeleteSharePairAsync(access.List.OwnerId, listId, memberUserId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task LeaveAsync(string userId, string listId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);
        if (access.IsOwner)
        {
            throw new ArgumentException("List owners cannot leave their own list. Delete it instead.");
        }

        await DeleteSharePairAsync(access.List.OwnerId, listId, userId, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ListShareDbo>> GetSharesForMemberAsync(
        string memberUserId,
        CancellationToken cancellationToken = default) =>
        _listShareRepository.GetByMemberAsync(memberUserId, cancellationToken);

    public async Task RemoveAllSharesForListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var shares = await _listShareRepository
            .GetByListAsync(listOwnerId, listId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var share in shares)
        {
            await DeleteSharePairAsync(listOwnerId, listId, share.MemberUserId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task SyncListNameAsync(
        string listOwnerId,
        string listId,
        string listName,
        CancellationToken cancellationToken = default)
    {
        var shares = await _listShareRepository
            .GetByListAsync(listOwnerId, listId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var share in shares)
        {
            share.ListName = listName;
            await _listShareRepository.UpsertAsync(share, cancellationToken).ConfigureAwait(false);

            var memberCopy = await _listShareRepository
                .GetAsync(share.MemberUserId, share.Id, cancellationToken)
                .ConfigureAwait(false);
            if (memberCopy is not null)
            {
                memberCopy.ListName = listName;
                await _listShareRepository.UpsertAsync(memberCopy, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DeleteSharePairAsync(
        string listOwnerId,
        string listId,
        string memberUserId,
        CancellationToken cancellationToken)
    {
        var shareId = BuildShareId(listId, memberUserId);
        await _listShareRepository.DeleteAsync(listOwnerId, shareId, cancellationToken).ConfigureAwait(false);
        await _listShareRepository.DeleteAsync(memberUserId, shareId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserDto> ResolveUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalised = StringUtilities.NormalizeWhitespace(username).TrimStart('@');
        if (string.IsNullOrWhiteSpace(normalised))
        {
            throw new ArgumentException("Username is required.");
        }

        var user = await _userService.GetByUsernameAsync(normalised, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new ArgumentException($"User '{normalised}' was not found.");
        }

        return user;
    }

    private static string BuildShareId(string listId, string memberUserId) =>
        $"share:{listId}:{memberUserId}";

    private static string NormaliseRole(string? role)
    {
        var value = string.IsNullOrWhiteSpace(role)
            ? ListShareRoles.Editor
            : role.Trim().ToLowerInvariant();

        if (!ListShareRoles.IsValidMemberRole(value))
        {
            throw new ArgumentException("Role must be 'editor' or 'viewer'.");
        }

        return value;
    }

    private static string? NormaliseOptional(string? value)
    {
        var normalised = StringUtilities.NormalizeWhitespace(value);
        return string.IsNullOrWhiteSpace(normalised) ? null : normalised;
    }

    private static ListShareDbo CloneShare(ListShareDbo source, string partitionOwnerId) =>
        new()
        {
            Id = source.Id,
            RecordType = ListRecordTypes.ListShare,
            OwnerId = partitionOwnerId,
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

    private static ListShareMemberDto MapMember(ListShareDbo share) =>
        new()
        {
            UserId = share.MemberUserId,
            Username = share.MemberUsername,
            DisplayName = share.MemberDisplayName,
            Role = share.Role,
            ShareLabel = share.ShareLabel,
            Added = share.Added
        };
}
