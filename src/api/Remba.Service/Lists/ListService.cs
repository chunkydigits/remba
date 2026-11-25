using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Repository.Lists;
using Remba.Utility;

namespace Remba.Service.Lists;

public class ListService : IListService
{
    private readonly IListRepository _listRepository;
    private readonly IListShareService _listShareService;
    private readonly ILogger<ListService> _logger;

    public ListService(
        IListRepository listRepository,
        IListShareService listShareService,
        ILogger<ListService> logger)
    {
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
        _listShareService = listShareService ?? throw new ArgumentNullException(nameof(listShareService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ListDto> CreateAsync(
        string ownerId,
        CreateListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (StringUtilities.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        var name = StringUtilities.NormalizeWhitespace(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("List name is required.");
        }

        var visibility = NormaliseVisibility(request.Visibility);
        var now = DateTime.UtcNow;
        var created = await _listRepository.CreateAsync(
                new ListDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RecordType = ListRecordTypes.List,
                    OwnerId = ownerId,
                    Visibility = visibility,
                    Name = name,
                    Description = StringUtilities.NormalizeWhitespace(request.Description),
                    ReferralLink = NormaliseReferralLink(request.ReferralLink),
                    Configuration = new ListConfigurationDbo
                    {
                        EntityType = NormaliseOptional(request.EntityType),
                        Attributes = []
                    },
                    ViewSettings = new ListViewSettingsDbo(),
                    ExternalLinks = [],
                    Added = now,
                    AddedByUserId = ownerId,
                    Edited = null,
                    ChangedByUserId = null
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Created list {ListId} for owner {OwnerId}", created.Id, ownerId);
        return await MapToDtoAsync(created, ListShareRoles.Owner, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ListDto?> GetOwnAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        var access = await _listShareService
            .TryResolveAccessAsync(ownerId, listId, cancellationToken)
            .ConfigureAwait(false);
        if (access is null)
        {
            return null;
        }

        return await MapToDtoAsync(access.List, access.Role, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ListSummaryDto>> GetOwnListsAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var owned = await _listRepository.GetByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var summaries = owned
            .Select(list => MapToSummary(list, ListShareRoles.Owner, shareLabel: null))
            .ToList();

        var shares = await _listShareService
            .GetSharesForMemberAsync(ownerId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var share in shares)
        {
            if (summaries.Any(summary => string.Equals(summary.Id, share.ListId, StringComparison.Ordinal)))
            {
                continue;
            }

            var list = await _listRepository
                .GetByIdAsync(share.ListOwnerId, share.ListId, cancellationToken)
                .ConfigureAwait(false);
            if (list is null)
            {
                continue;
            }

            summaries.Add(MapToSummary(list, share.Role, share.ShareLabel));
        }

        return summaries
            .OrderByDescending(summary => summary.Edited ?? summary.Added)
            .ToList();
    }

    public async Task<ListDto> UpdateAsync(
        string ownerId,
        string listId,
        UpdateListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await RequireManageListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var list = access.List;
        var nameChanged = false;

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("List name cannot be empty.");
            }

            nameChanged = !string.Equals(list.Name, name, StringComparison.Ordinal);
            list.Name = name;
        }

        if (request.Description is not null)
        {
            list.Description = StringUtilities.NormalizeWhitespace(request.Description);
        }

        if (request.ReferralLink is not null)
        {
            list.ReferralLink = NormaliseReferralLink(request.ReferralLink);
        }

        if (request.EntityType is not null)
        {
            list.Configuration.EntityType = NormaliseOptional(request.EntityType);
        }

        if (request.Visibility is not null)
        {
            list.Visibility = NormaliseVisibility(request.Visibility);
        }

        if (request.Attributes is not null)
        {
            list.Configuration ??= new ListConfigurationDbo();
            list.Configuration.Attributes = NormaliseAttributes(request.Attributes);
            EnsureCustomAttributesVisible(list);
        }

        list.Edited = DateTime.UtcNow;
        list.ChangedByUserId = ownerId;

        var updated = await _listRepository.UpdateAsync(list, cancellationToken).ConfigureAwait(false);

        if (nameChanged)
        {
            await _listShareService
                .SyncListNameAsync(updated.OwnerId, updated.Id, updated.Name, cancellationToken)
                .ConfigureAwait(false);
        }

        return await MapToDtoAsync(updated, ListShareRoles.Owner, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ListDto> UpdateViewSettingsAsync(
        string ownerId,
        string listId,
        UpdateListViewSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // List-level view settings stay owner-managed until per-user settings exist.
        var access = await RequireManageListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var list = access.List;

        if (request.SortBy is not null)
        {
            var sortBy = request.SortBy.Trim();
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                throw new ArgumentException("Sort by cannot be empty.");
            }

            list.ViewSettings.SortBy = sortBy;
        }

        if (request.SortDirection is not null)
        {
            list.ViewSettings.SortDirection = NormaliseSortDirection(request.SortDirection);
        }

        if (request.ViewMode is not null)
        {
            list.ViewSettings.ViewMode = NormaliseViewMode(request.ViewMode);
        }

        if (request.VisibleFields is not null)
        {
            list.ViewSettings.VisibleFields = request.VisibleFields
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(field => field.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        list.Edited = DateTime.UtcNow;
        list.ChangedByUserId = ownerId;

        var updated = await _listRepository.UpdateAsync(list, cancellationToken).ConfigureAwait(false);
        return await MapToDtoAsync(updated, ListShareRoles.Owner, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        var access = await RequireManageListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        await _listShareService
            .RemoveAllSharesForListAsync(access.List.OwnerId, listId, cancellationToken)
            .ConfigureAwait(false);
        await _listRepository.DeleteAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted list {ListId} for owner {OwnerId}", listId, ownerId);
    }

    private async Task<ListAccessContext> RequireManageListAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken)
    {
        var access = await _listShareService
            .RequireAccessAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);

        if (!ListShareRoles.CanManageList(access.Role))
        {
            throw new UnauthorizedAccessException("Only the list owner can change list settings.");
        }

        return access;
    }

    private async Task<ListDto> MapToDtoAsync(
        ListDbo list,
        string role,
        CancellationToken cancellationToken)
    {
        var configuration = list.Configuration ?? new ListConfigurationDbo();
        var attributes = configuration.Attributes ?? [];
        var viewSettings = list.ViewSettings ?? new ListViewSettingsDbo();

        var members = await _listShareService
            .GetMembersAsync(list.OwnerId, list.Id, cancellationToken)
            .ConfigureAwait(false);

        var shareLabel = members
            .Select(member => member.ShareLabel)
            .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));

        return new ListDto
        {
            Id = list.Id,
            OwnerId = list.OwnerId,
            Visibility = list.Visibility,
            Name = list.Name,
            Description = list.Description,
            ReferralLink = list.ReferralLink,
            EntityType = configuration.EntityType,
            Attributes = attributes
                .Select(attribute => new ListAttributeDefinitionDto
                {
                    Key = attribute.Key,
                    Name = attribute.Name,
                    Description = attribute.Description,
                    Type = attribute.Type,
                    Scope = attribute.Scope
                })
                .ToList(),
            ViewSettings = new ListViewSettingsDto
            {
                SortBy = viewSettings.SortBy,
                SortDirection = viewSettings.SortDirection,
                ViewMode = viewSettings.ViewMode,
                VisibleFields = [.. viewSettings.VisibleFields ?? []]
            },
            MyRole = role,
            IsOwned = string.Equals(role, ListShareRoles.Owner, StringComparison.Ordinal),
            ShareLabel = shareLabel,
            Members = members.ToList(),
            Added = list.Added,
            Edited = list.Edited
        };
    }

    private static ListSummaryDto MapToSummary(ListDbo list, string role, string? shareLabel) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            Visibility = list.Visibility,
            EntityType = list.Configuration?.EntityType,
            ReferralLink = list.ReferralLink,
            MyRole = role,
            IsOwned = string.Equals(role, ListShareRoles.Owner, StringComparison.Ordinal),
            ShareLabel = shareLabel,
            Added = list.Added,
            Edited = list.Edited
        };

    private static string NormaliseVisibility(string? visibility)
    {
        var value = string.IsNullOrWhiteSpace(visibility)
            ? ListVisibility.Private
            : visibility.Trim().ToLowerInvariant();

        if (value is not ListVisibility.Private and not ListVisibility.Public)
        {
            throw new ArgumentException("Visibility must be 'private' or 'public'.");
        }

        return value;
    }

    private static string NormaliseSortDirection(string sortDirection)
    {
        var value = sortDirection.Trim().ToLowerInvariant();
        if (value is not ListSortDirections.Ascending and not ListSortDirections.Descending)
        {
            throw new ArgumentException("Sort direction must be 'ascending' or 'descending'.");
        }

        return value;
    }

    private static string NormaliseViewMode(string viewMode)
    {
        var value = viewMode.Trim().ToLowerInvariant();
        if (value is not ListViewModes.List and not ListViewModes.Grid)
        {
            throw new ArgumentException("View mode must be 'list' or 'grid'.");
        }

        return value;
    }

    private static string? NormaliseOptional(string? value)
    {
        var normalised = StringUtilities.NormalizeWhitespace(value);
        return string.IsNullOrWhiteSpace(normalised) ? null : normalised;
    }

    private static List<ListAttributeDefinitionDbo> NormaliseAttributes(
        IEnumerable<ListAttributeDefinitionRequest> attributes)
    {
        var normalised = new List<ListAttributeDefinitionDbo>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in attributes)
        {
            var name = StringUtilities.NormalizeWhitespace(attribute.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Property name is required.");
            }

            var type = NormaliseAttributeType(attribute.Type);
            var key = StringUtilities.NormalizeWhitespace(attribute.Key);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = BuildAttributeKey(name);
            }
            else
            {
                key = BuildAttributeKey(key);
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"Unable to derive a key for property '{name}'.");
            }

            if (!usedKeys.Add(key))
            {
                throw new ArgumentException($"Duplicate property key '{key}'.");
            }

            normalised.Add(
                new ListAttributeDefinitionDbo
                {
                    Key = key,
                    Name = name,
                    Description = NormaliseOptional(attribute.Description),
                    Type = type,
                    Scope = "entry"
                });
        }

        return normalised;
    }

    private static string NormaliseAttributeType(string? type)
    {
        var value = (type ?? ListAttributeTypes.Text).Trim().ToLowerInvariant();
        if (value is "truefalse" or "true-false" or "true/false")
        {
            value = ListAttributeTypes.Boolean;
        }

        if (value is "chips" or "tags" or "comma-separated" or "csv")
        {
            value = ListAttributeTypes.List;
        }

        if (value != ListAttributeTypes.Text
            && value != ListAttributeTypes.Number
            && value != ListAttributeTypes.Boolean
            && value != ListAttributeTypes.Url
            && value != ListAttributeTypes.List)
        {
            throw new ArgumentException("Property type must be text, number, boolean, url, or list.");
        }

        return value;
    }

    private static string BuildAttributeKey(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var key = new string(chars);
        while (key.Contains("--", StringComparison.Ordinal))
        {
            key = key.Replace("--", "-", StringComparison.Ordinal);
        }

        return key.Trim('-');
    }

    private static void EnsureCustomAttributesVisible(ListDbo list)
    {
        list.ViewSettings ??= new ListViewSettingsDbo();
        list.ViewSettings.VisibleFields ??= [];

        var visible = list.ViewSettings.VisibleFields;
        foreach (var attribute in list.Configuration.Attributes ?? [])
        {
            if (visible.All(field => !string.Equals(field, attribute.Key, StringComparison.OrdinalIgnoreCase)))
            {
                visible.Add(attribute.Key);
            }
        }
    }

    private static string? NormaliseReferralLink(string? referralLink)
    {
        var value = StringUtilities.NormalizeWhitespace(referralLink);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Referral link must be an absolute http or https URL.");
        }

        return uri.AbsoluteUri;
    }
}
