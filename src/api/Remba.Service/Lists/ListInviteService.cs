using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Repository.Lists;
using Remba.Service.Users;
using Remba.Utility;

namespace Remba.Service.Lists;

public class ListInviteService : IListInviteService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IListInviteRepository _listInviteRepository;
    private readonly IListShareService _listShareService;
    private readonly IListRepository _listRepository;
    private readonly IUserService _userService;
    private readonly ILogger<ListInviteService> _logger;

    public ListInviteService(
        IListInviteRepository listInviteRepository,
        IListShareService listShareService,
        IListRepository listRepository,
        IUserService userService,
        ILogger<ListInviteService> logger)
    {
        _listInviteRepository = listInviteRepository
                                ?? throw new ArgumentNullException(nameof(listInviteRepository));
        _listShareService = listShareService ?? throw new ArgumentNullException(nameof(listShareService));
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ListInviteDto>> GetByListAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var access = await _listShareService.RequireAccessAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can view invite codes.");
        }

        var invites = await _listInviteRepository
            .GetByListAsync(access.List.OwnerId, listId, cancellationToken)
            .ConfigureAwait(false);

        return invites.Select(MapToDto).ToList();
    }

    public async Task<ListInviteDto> CreateAsync(
        string userId,
        string listId,
        CreateListInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await _listShareService.RequireAccessAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can create invite codes.");
        }

        var role = NormaliseRole(request.Role);
        var code = await CreateUniqueCodeAsync(cancellationToken).ConfigureAwait(false);
        var invite = await _listInviteRepository
            .CreateAsync(
                new ListInviteDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RecordType = ListRecordTypes.ListInvite,
                    OwnerId = access.List.OwnerId,
                    ListId = access.List.Id,
                    ListOwnerId = access.List.OwnerId,
                    ListName = access.List.Name,
                    Code = code,
                    Role = role,
                    Added = DateTime.UtcNow,
                    AddedByUserId = userId
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created invite {InviteId} for list {ListId} by {UserId}",
            invite.Id,
            listId,
            userId);

        return MapToDto(invite);
    }

    public async Task DeleteAsync(
        string userId,
        string listId,
        string inviteId,
        CancellationToken cancellationToken = default)
    {
        var access = await _listShareService.RequireAccessAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (!ListShareRoles.CanManageShares(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can delete invite codes.");
        }

        var invite = await _listInviteRepository
            .GetByIdAsync(access.List.OwnerId, inviteId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Invite was not found.");

        if (!string.Equals(invite.ListId, listId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException("Invite was not found.");
        }

        if (invite.UsedAt is not null)
        {
            throw new InvalidOperationException("Used invite codes cannot be deleted.");
        }

        await _listInviteRepository
            .DeleteAsync(access.List.OwnerId, inviteId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RedeemListInviteResponse> RedeemAsync(
        string userId,
        RedeemListInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = NormaliseCode(request.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Invite code is required.");
        }

        var invite = await _listInviteRepository.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false)
                     ?? throw new KeyNotFoundException("Invite code was not found.");

        if (invite.UsedAt is not null)
        {
            throw new InvalidOperationException("This invite code has already been used.");
        }

        if (string.Equals(invite.ListOwnerId, userId, StringComparison.Ordinal))
        {
            throw new ArgumentException("You already own this list.");
        }

        var existingAccess = await _listShareService
            .TryResolveAccessAsync(userId, invite.ListId, cancellationToken)
            .ConfigureAwait(false);
        if (existingAccess is not null)
        {
            throw new InvalidOperationException("You already have access to this list.");
        }

        var list = await _listRepository
            .GetByIdAsync(invite.ListOwnerId, invite.ListId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("List was not found.");

        var user = await _userService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("User was not found.");

        await _listShareService
            .GrantShareAsync(
                list,
                user,
                invite.Role,
                invite.AddedByUserId,
                shareLabel: null,
                cancellationToken)
            .ConfigureAwait(false);

        invite.UsedAt = DateTime.UtcNow;
        invite.UsedByUserId = user.Id;
        invite.UsedByUsername = user.Username;
        invite.UsedByDisplayName = user.DisplayName;
        invite.ListName = list.Name;

        await _listInviteRepository.UpdateAsync(invite, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Invite {InviteId} redeemed by {UserId} for list {ListId}",
            invite.Id,
            userId,
            invite.ListId);

        return new RedeemListInviteResponse
        {
            ListId = list.Id,
            ListName = list.Name,
            Role = invite.Role
        };
    }

    private async Task<string> CreateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var code = GenerateCode();
            var existing = await _listInviteRepository.GetByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique invite code.");
    }

    private static string GenerateCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return $"{new string(chars[..4])}-{new string(chars[4..])}";
    }

    private static string NormaliseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var cleaned = new string(code.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)).ToArray())
            .ToUpperInvariant();
        if (cleaned.Length != 8)
        {
            return code.Trim().ToUpperInvariant();
        }

        return $"{cleaned[..4]}-{cleaned[4..]}";
    }

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

    private static ListInviteDto MapToDto(ListInviteDbo invite) =>
        new()
        {
            Id = invite.Id,
            ListId = invite.ListId,
            ListName = invite.ListName,
            Code = invite.Code,
            Role = invite.Role,
            Added = invite.Added,
            IsUsed = invite.UsedAt is not null,
            UsedAt = invite.UsedAt,
            UsedByUserId = invite.UsedByUserId,
            UsedByUsername = invite.UsedByUsername,
            UsedByDisplayName = invite.UsedByDisplayName
        };
}
