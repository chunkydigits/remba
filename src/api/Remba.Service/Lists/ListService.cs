using Microsoft.Extensions.Logging;
using Remba.Model.Lists;
using Remba.Repository.Lists;
using Remba.Utility;

namespace Remba.Service.Lists;

public class ListService : IListService
{
    private readonly IListRepository _listRepository;
    private readonly ILogger<ListService> _logger;

    public ListService(IListRepository listRepository, ILogger<ListService> logger)
    {
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
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
        return MapToDto(created);
    }

    public async Task<ListDto?> GetOwnAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        var list = await _listRepository.GetByIdAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        return list is null ? null : MapToDto(list);
    }

    public async Task<IReadOnlyList<ListSummaryDto>> GetOwnListsAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var lists = await _listRepository.GetByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return lists.Select(MapToSummary).ToList();
    }

    public async Task<ListDto> UpdateAsync(
        string ownerId,
        string listId,
        UpdateListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var list = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("List name cannot be empty.");
            }

            list.Name = name;
        }

        if (request.Description is not null)
        {
            list.Description = StringUtilities.NormalizeWhitespace(request.Description);
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
            list.Configuration.Attributes = NormaliseAttributes(request.Attributes);
        }

        list.Edited = DateTime.UtcNow;
        list.ChangedByUserId = ownerId;

        var updated = await _listRepository.UpdateAsync(list, cancellationToken).ConfigureAwait(false);
        return MapToDto(updated);
    }

    public async Task<ListDto> UpdateViewSettingsAsync(
        string ownerId,
        string listId,
        UpdateListViewSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var list = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);

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
        return MapToDto(updated);
    }

    public async Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        await _listRepository.DeleteAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted list {ListId} for owner {OwnerId}", listId, ownerId);
    }

    private async Task<ListDbo> RequireOwnedListAsync(
        string ownerId,
        string listId,
        CancellationToken cancellationToken)
    {
        var list = await _listRepository.GetByIdAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        if (list is null)
        {
            throw new KeyNotFoundException("List was not found.");
        }

        return list;
    }

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

        if (value != ListAttributeTypes.Text
            && value != ListAttributeTypes.Number
            && value != ListAttributeTypes.Boolean
            && value != ListAttributeTypes.Url)
        {
            throw new ArgumentException("Property type must be text, number, boolean, or url.");
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

    private static ListDto MapToDto(ListDbo list) =>
        new()
        {
            Id = list.Id,
            OwnerId = list.OwnerId,
            Visibility = list.Visibility,
            Name = list.Name,
            Description = list.Description,
            EntityType = list.Configuration.EntityType,
            Attributes = list.Configuration.Attributes
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
                SortBy = list.ViewSettings.SortBy,
                SortDirection = list.ViewSettings.SortDirection,
                ViewMode = list.ViewSettings.ViewMode,
                VisibleFields = [.. list.ViewSettings.VisibleFields]
            },
            Added = list.Added,
            Edited = list.Edited
        };

    private static ListSummaryDto MapToSummary(ListDbo list) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            Visibility = list.Visibility,
            EntityType = list.Configuration.EntityType,
            Added = list.Added,
            Edited = list.Edited
        };
}
