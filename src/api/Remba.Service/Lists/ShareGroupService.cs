using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Repository.Lists;
using Remba.Service.Users;
using Remba.Utility;

namespace Remba.Service.Lists;

public class ShareGroupService : IShareGroupService
{
    private readonly IShareGroupRepository _shareGroupRepository;
    private readonly IUserService _userService;
    private readonly ILogger<ShareGroupService> _logger;

    public ShareGroupService(
        IShareGroupRepository shareGroupRepository,
        IUserService userService,
        ILogger<ShareGroupService> logger)
    {
        _shareGroupRepository = shareGroupRepository
                                ?? throw new ArgumentNullException(nameof(shareGroupRepository));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ShareGroupDto>> GetOwnAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var groups = await _shareGroupRepository
            .GetByOwnerAsync(ownerId, cancellationToken)
            .ConfigureAwait(false);
        return groups.Select(MapToDto).ToList();
    }

    public async Task<ShareGroupDto> CreateAsync(
        string ownerId,
        CreateShareGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = StringUtilities.NormalizeWhitespace(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name is required.");
        }

        var members = await ResolveMembersAsync(ownerId, request.Usernames, cancellationToken)
            .ConfigureAwait(false);

        var created = await _shareGroupRepository
            .CreateAsync(
                new ShareGroupDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RecordType = ListRecordTypes.ShareGroup,
                    OwnerId = ownerId,
                    Name = name,
                    Members = members,
                    Added = DateTime.UtcNow,
                    AddedByUserId = ownerId
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Created share group {GroupId} for owner {OwnerId}", created.Id, ownerId);
        return MapToDto(created);
    }

    public async Task<ShareGroupDto> UpdateAsync(
        string ownerId,
        string groupId,
        UpdateShareGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = await _shareGroupRepository
            .GetByIdAsync(ownerId, groupId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Share group was not found.");

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Group name cannot be empty.");
            }

            group.Name = name;
        }

        if (request.Usernames is not null)
        {
            group.Members = await ResolveMembersAsync(ownerId, request.Usernames, cancellationToken)
                .ConfigureAwait(false);
        }

        group.Edited = DateTime.UtcNow;
        group.ChangedByUserId = ownerId;

        var updated = await _shareGroupRepository.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(string ownerId, string groupId, CancellationToken cancellationToken = default)
    {
        _ = await _shareGroupRepository.GetByIdAsync(ownerId, groupId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Share group was not found.");

        await _shareGroupRepository.DeleteAsync(ownerId, groupId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted share group {GroupId} for owner {OwnerId}", groupId, ownerId);
    }

    public Task<ShareGroupDbo?> GetOwnedGroupAsync(
        string ownerId,
        string groupId,
        CancellationToken cancellationToken = default) =>
        _shareGroupRepository.GetByIdAsync(ownerId, groupId, cancellationToken);

    private async Task<List<ShareGroupMemberDbo>> ResolveMembersAsync(
        string ownerId,
        IEnumerable<string>? usernames,
        CancellationToken cancellationToken)
    {
        var members = new Dictionary<string, ShareGroupMemberDbo>(StringComparer.Ordinal);

        foreach (var username in usernames ?? [])
        {
            var normalised = StringUtilities.NormalizeWhitespace(username).TrimStart('@');
            if (string.IsNullOrWhiteSpace(normalised))
            {
                continue;
            }

            var user = await _userService.GetByUsernameAsync(normalised, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                throw new ArgumentException($"User '{normalised}' was not found.");
            }

            if (string.Equals(user.Id, ownerId, StringComparison.Ordinal))
            {
                continue;
            }

            members[user.Id] = new ShareGroupMemberDbo
            {
                UserId = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName
            };
        }

        return members.Values.ToList();
    }

    private static ShareGroupDto MapToDto(ShareGroupDbo group) =>
        new()
        {
            Id = group.Id,
            Name = group.Name,
            Members = group.Members
                .Select(member => new ShareGroupMemberDto
                {
                    UserId = member.UserId,
                    Username = member.Username,
                    DisplayName = member.DisplayName
                })
                .ToList(),
            Added = group.Added,
            Edited = group.Edited
        };
}
