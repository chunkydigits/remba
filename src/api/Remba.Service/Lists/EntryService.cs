using System.Text.Json;
using Microsoft.Extensions.Logging;
using Remba.Model.Entities;
using Remba.Model.Lists;
using Remba.Repository.Entities;
using Remba.Repository.Lists;
using Remba.Service.Entities;
using Remba.Service.Users;
using Remba.Utility;

namespace Remba.Service.Lists;

public class EntryService : IEntryService
{
    private readonly IEntryRepository _entryRepository;
    private readonly IEntityRepository _entityRepository;
    private readonly IEntityLookupService _entityLookupService;
    private readonly IListShareService _listShareService;
    private readonly IUserService _userService;
    private readonly ILogger<EntryService> _logger;

    public EntryService(
        IEntryRepository entryRepository,
        IEntityRepository entityRepository,
        IEntityLookupService entityLookupService,
        IListShareService listShareService,
        IUserService userService,
        ILogger<EntryService> logger)
    {
        _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        _entityRepository = entityRepository ?? throw new ArgumentNullException(nameof(entityRepository));
        _entityLookupService = entityLookupService ?? throw new ArgumentNullException(nameof(entityLookupService));
        _listShareService = listShareService ?? throw new ArgumentNullException(nameof(listShareService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EntryDto> CreateAsync(
        string ownerId,
        string listId,
        CreateEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (StringUtilities.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        if (StringUtilities.IsNullOrWhiteSpace(listId))
        {
            throw new ArgumentException("List id is required.", nameof(listId));
        }

        var access = await RequireEditableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var list = access.List;
        var listOwnerId = list.OwnerId;
        var enrichment = await ResolveEnrichmentAsync(list, request, cancellationToken).ConfigureAwait(false);

        var name = enrichment.ResolvedName
            ?? StringUtilities.NormalizeWhitespace(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            var fallbackIdentifier = StringUtilities.NormalizeWhitespace(request.Identifier?.Value);
            if (!string.IsNullOrWhiteSpace(fallbackIdentifier))
            {
                name = fallbackIdentifier;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Entry name is required (or provide an identifier that can be resolved).");
        }

        var now = DateTime.UtcNow;

        var entity = await ResolveEntityForCreateAsync(
                listOwnerId,
                list,
                name,
                request,
                enrichment,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var existingEntry = await _entryRepository
            .FindActiveByEntityAsync(listOwnerId, listId, entity.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existingEntry is not null)
        {
            throw new InvalidOperationException("That item is already on this list.");
        }

        var order = await _entryRepository
            .GetNextOrderAsync(listOwnerId, listId, cancellationToken)
            .ConfigureAwait(false);

        var isBook = string.Equals(list.Configuration.EntityType, EntityTypes.Book, StringComparison.OrdinalIgnoreCase);
        var isCountry = string.Equals(list.Configuration.EntityType, EntityTypes.Country, StringComparison.OrdinalIgnoreCase);
        var isWatchable = IsWatchableEntityType(list.Configuration.EntityType);
        var identifierType = StringUtilities.NormalizeWhitespace(request.Identifier?.Type)
            ?? entity.Identifiers.FirstOrDefault()?.Type;
        var identifierValue = StringUtilities.NormalizeWhitespace(request.Identifier?.Value)
            ?? entity.Identifiers.FirstOrDefault()?.Value;

        var seen = isWatchable ? request.Seen ?? false : request.Seen;
        var seenAt = ResolveSeenAt(seen, request.SeenAt, existingSeenAt: null, now);

        var actor = await _userService.GetByIdAsync(ownerId, cancellationToken).ConfigureAwait(false);

        var entry = await _entryRepository
            .CreateAsync(
                new EntryDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RecordType = ListRecordTypes.Entry,
                    ListId = listId,
                    OwnerId = listOwnerId,
                    EntityId = entity.Id,
                    IdentifierType = NormaliseOptional(identifierType),
                    IdentifierValue = NormaliseOptional(identifierValue),
                    Name = entity.Name,
                    Link = entity.Link ?? NormaliseOptional(request.Link),
                    ImageUrl = enrichment.ImageUrl
                        ?? NormaliseOptional(request.ImageUrl)
                        ?? GetMetadata(entity.Metadata, "imageUrl"),
                    Author = enrichment.Author,
                    Director = enrichment.Director,
                    Actors = enrichment.Actors,
                    Genre = enrichment.Genre,
                    Themes = enrichment.Themes,
                    Read = isBook ? request.Read ?? false : request.Read,
                    Visited = isCountry ? request.Visited ?? false : request.Visited,
                    Seen = seen,
                    SeenAt = seenAt,
                    Order = order,
                    Attributes = CloneAttributes(request.Attributes),
                    Added = now,
                    AddedByUserId = ownerId,
                    AddedByUsername = actor?.Username,
                    AddedByDisplayName = actor?.DisplayName,
                    Edited = null,
                    Deleted = null,
                    DeletedByUserId = null
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created entry {EntryId} on list {ListId} for owner {OwnerId}",
            entry.Id,
            listId,
            ownerId);

        return MapToDto(entry);
    }

    public async Task<IReadOnlyList<EntryDto>> GetByListAsync(
        string ownerId,
        string listId,
        EntryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireReadableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var listOwnerId = access.List.OwnerId;
        var entries = await _entryRepository
            .GetByListAsync(listOwnerId, listId, includeDeleted: false, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<EntryDbo> filtered = entries;

        if (query is not null)
        {
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var q = query.Q.Trim();
                filtered = filtered.Where(entry =>
                    Contains(entry.Name, q)
                    || Contains(entry.Author, q)
                    || Contains(entry.Director, q)
                    || Contains(entry.Actors, q)
                    || Contains(entry.Genre, q)
                    || Contains(entry.Themes, q)
                    || Contains(entry.Link, q));
            }

            if (!string.IsNullOrWhiteSpace(query.Author))
            {
                filtered = filtered.Where(entry => Contains(entry.Author, query.Author));
            }

            if (!string.IsNullOrWhiteSpace(query.Director))
            {
                filtered = filtered.Where(entry => Contains(entry.Director, query.Director));
            }

            if (!string.IsNullOrWhiteSpace(query.Actors))
            {
                filtered = filtered.Where(entry => Contains(entry.Actors, query.Actors));
            }

            if (!string.IsNullOrWhiteSpace(query.Genre))
            {
                filtered = filtered.Where(entry => Contains(entry.Genre, query.Genre));
            }

            if (!string.IsNullOrWhiteSpace(query.Themes))
            {
                filtered = filtered.Where(entry => Contains(entry.Themes, query.Themes));
            }

            if (query.Read is not null)
            {
                filtered = filtered.Where(entry => entry.Read == query.Read);
            }

            if (query.Visited is not null)
            {
                filtered = filtered.Where(entry => entry.Visited == query.Visited);
            }

            if (query.Seen is not null)
            {
                filtered = filtered.Where(entry => entry.Seen == query.Seen);
            }

            filtered = ApplySort(filtered, query.SortBy, query.SortDirection);
        }
        else
        {
            filtered = filtered.OrderBy(entry => entry.Order);
        }

        var mapped = new List<EntryDto>();
        foreach (var entry in filtered)
        {
            mapped.Add(await MapToDtoAsync(entry, cancellationToken).ConfigureAwait(false));
        }

        return mapped;
    }

    public async Task<EntryDto?> GetOwnAsync(
        string ownerId,
        string listId,
        string entryId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireReadableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var listOwnerId = access.List.OwnerId;
        var entry = await _entryRepository.GetByIdAsync(listOwnerId, entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null
            || !string.Equals(entry.ListId, listId, StringComparison.Ordinal)
            || entry.Deleted is not null)
        {
            return null;
        }

        return MapToDto(entry);
    }

    public async Task<EntryDto> UpdateAsync(
        string ownerId,
        string listId,
        string entryId,
        UpdateEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await RequireEditableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var listOwnerId = access.List.OwnerId;
        var entry = await RequireOwnedActiveEntryAsync(listOwnerId, listId, entryId, cancellationToken)
            .ConfigureAwait(false);

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Entry name cannot be empty.");
            }

            entry.Name = name;
        }

        if (request.Link is not null)
        {
            entry.Link = NormaliseOptional(request.Link);
        }

        if (request.ImageUrl is not null)
        {
            entry.ImageUrl = NormaliseOptional(request.ImageUrl);
        }

        if (request.Author is not null)
        {
            entry.Author = NormaliseOptional(request.Author);
        }

        if (request.Director is not null)
        {
            entry.Director = NormaliseOptional(request.Director);
        }

        if (request.Actors is not null)
        {
            entry.Actors = NormaliseOptional(request.Actors);
        }

        if (request.Genre is not null)
        {
            entry.Genre = NormaliseOptional(request.Genre);
        }

        if (request.Themes is not null)
        {
            entry.Themes = NormaliseOptional(request.Themes);
        }

        if (request.Read is not null)
        {
            entry.Read = request.Read;
        }

        if (request.Visited is not null)
        {
            entry.Visited = request.Visited;
        }

        if (request.Seen is not null || request.SeenAt is not null)
        {
            var seen = request.Seen ?? entry.Seen;
            entry.Seen = seen;
            entry.SeenAt = ResolveSeenAt(seen, request.SeenAt, entry.SeenAt, DateTime.UtcNow);
        }

        if (request.Attributes is not null)
        {
            entry.Attributes = CloneAttributes(request.Attributes);
        }

        entry.Edited = DateTime.UtcNow;

        await SyncEntityCatalogAsync(ownerId, entry, request, cancellationToken).ConfigureAwait(false);

        var updated = await _entryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        return MapToDto(updated);
    }

    private async Task SyncEntityCatalogAsync(
        string ownerId,
        EntryDbo entry,
        UpdateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var shouldSync = request.Name is not null
            || request.Link is not null
            || request.ImageUrl is not null
            || request.Author is not null
            || request.Director is not null
            || request.Actors is not null
            || request.Genre is not null
            || request.Themes is not null;

        if (!shouldSync || string.IsNullOrWhiteSpace(entry.EntityId))
        {
            return;
        }

        var entity = await _entityRepository.GetByIdAsync(entry.EntityId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        var changed = false;
        if (request.Name is not null && !string.Equals(entity.Name, entry.Name, StringComparison.Ordinal))
        {
            entity.Name = entry.Name;
            changed = true;
        }

        if (request.Link is not null)
        {
            entity.Link = entry.Link;
            changed = true;
        }

        changed |= SetEntityMetadata(entity, "imageUrl", entry.ImageUrl);
        changed |= SetEntityMetadata(entity, "author", entry.Author);
        changed |= SetEntityMetadata(entity, "director", entry.Director);
        changed |= SetEntityMetadata(entity, "actors", entry.Actors);
        changed |= SetEntityMetadata(entity, "genre", entry.Genre);
        changed |= SetEntityMetadata(entity, "themes", entry.Themes);

        if (!changed)
        {
            return;
        }

        entity.Edited = DateTime.UtcNow;
        entity.ChangedByUserId = ownerId;
        await _entityRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    private static bool SetEntityMetadata(EntityDbo entity, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!entity.Metadata.Remove(key))
            {
                return false;
            }

            return true;
        }

        if (entity.Metadata.TryGetValue(key, out var existing)
            && string.Equals(existing, value, StringComparison.Ordinal))
        {
            return false;
        }

        entity.Metadata[key] = value;
        return true;
    }

    public async Task ReorderAsync(
        string ownerId,
        string listId,
        ReorderEntriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EntryIds.Count == 0)
        {
            throw new ArgumentException("At least one entry id is required.");
        }

        var access = await RequireEditableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var listOwnerId = access.List.OwnerId;
        var existing = await _entryRepository
            .GetByListAsync(listOwnerId, listId, includeDeleted: false, cancellationToken)
            .ConfigureAwait(false);
        var byId = existing.ToDictionary(entry => entry.Id, StringComparer.Ordinal);

        if (request.EntryIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new ArgumentException("One or more entries were not found on this list.");
        }

        if (request.EntryIds.Count != existing.Count
            || request.EntryIds.Distinct(StringComparer.Ordinal).Count() != request.EntryIds.Count)
        {
            throw new ArgumentException("Reorder must include each active list entry exactly once.");
        }

        for (var index = 0; index < request.EntryIds.Count; index++)
        {
            var entry = byId[request.EntryIds[index]];
            var newOrder = index + 1;
            if (entry.Order == newOrder)
            {
                continue;
            }

            entry.Order = newOrder;
            entry.Edited = DateTime.UtcNow;
            await _entryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SoftDeleteAsync(
        string ownerId,
        string listId,
        string entryId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireEditableListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var listOwnerId = access.List.OwnerId;
        var entry = await RequireOwnedActiveEntryAsync(listOwnerId, listId, entryId, cancellationToken)
            .ConfigureAwait(false);

        entry.Deleted = DateTime.UtcNow;
        entry.DeletedByUserId = ownerId;
        entry.Edited = entry.Deleted;

        await _entryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Soft-deleted entry {EntryId} on list {ListId} for owner {OwnerId}",
            entryId,
            listId,
            ownerId);
    }

    public async Task<IReadOnlyList<string>> GetListIdsContainingAsync(
        string ownerId,
        string? identifierType,
        string? identifierValue,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        var type = StringUtilities.NormalizeWhitespace(identifierType);
        var value = StringUtilities.NormalizeWhitespace(identifierValue);
        var resolvedEntityId = StringUtilities.NormalizeWhitespace(entityId);
        var normalisedType = string.IsNullOrEmpty(type) ? null : type;
        var normalisedValue = string.IsNullOrEmpty(value) ? null : value;
        var normalisedEntityId = string.IsNullOrEmpty(resolvedEntityId) ? null : resolvedEntityId;

        var listIds = new HashSet<string>(StringComparer.Ordinal);

        var ownedMatches = await _entryRepository
            .FindActiveListIdsContainingAsync(
                ownerId,
                normalisedType,
                normalisedValue,
                normalisedEntityId,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var listId in ownedMatches)
        {
            listIds.Add(listId);
        }

        var shares = await _listShareService
            .GetSharesForMemberAsync(ownerId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var share in shares)
        {
            var sharedMatches = await _entryRepository
                .FindActiveListIdsContainingAsync(
                    share.ListOwnerId,
                    normalisedType,
                    normalisedValue,
                    normalisedEntityId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sharedMatches.Contains(share.ListId, StringComparer.Ordinal))
            {
                listIds.Add(share.ListId);
            }
        }

        return listIds.ToList();
    }

    private async Task<EnrichmentFields> ResolveEnrichmentAsync(
        ListDbo list,
        CreateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var enrichment = new EnrichmentFields
        {
            Author = NormaliseOptional(request.Author) ?? NormaliseOptional(GetMetadata(request.Metadata, "author")),
            Director = NormaliseOptional(request.Director) ?? NormaliseOptional(GetMetadata(request.Metadata, "director")),
            Actors = NormaliseOptional(request.Actors) ?? NormaliseOptional(GetMetadata(request.Metadata, "actors")),
            Genre = NormaliseOptional(request.Genre) ?? NormaliseOptional(GetMetadata(request.Metadata, "genre")),
            Themes = NormaliseOptional(request.Themes) ?? NormaliseOptional(GetMetadata(request.Metadata, "themes")),
            Year = NormaliseOptional(GetMetadata(request.Metadata, "year")),
            ImageUrl = NormaliseOptional(request.ImageUrl)
                ?? NormaliseOptional(GetMetadata(request.Metadata, "imageUrl")),
            Link = NormaliseOptional(request.Link)
        };

        var identifierType = StringUtilities.NormalizeWhitespace(request.Identifier?.Type);
        var identifierValue = StringUtilities.NormalizeWhitespace(request.Identifier?.Value);
        var entityType = list.Configuration.EntityType;

        if (string.IsNullOrWhiteSpace(identifierType)
            || string.IsNullOrWhiteSpace(identifierValue)
            || string.IsNullOrWhiteSpace(entityType))
        {
            return enrichment;
        }

        var detail = await _entityLookupService
            .GetByIdentifierAsync(entityType, identifierType, identifierValue, cancellationToken)
            .ConfigureAwait(false);

        if (detail is null)
        {
            // Search payloads may already carry author (books) without a detail round-trip.
            enrichment.Author ??= NormaliseOptional(GetMetadata(request.Metadata, "subtitle"));
            return enrichment;
        }

        enrichment.Author ??= NormaliseOptional(detail.Author);
        enrichment.Director ??= NormaliseOptional(detail.Director);
        enrichment.Actors ??= NormaliseOptional(detail.Actors);
        enrichment.Genre ??= NormaliseOptional(detail.Genre);
        enrichment.Themes ??= NormaliseOptional(detail.Themes);
        enrichment.Year ??= NormaliseOptional(detail.Year);
        enrichment.ImageUrl ??= NormaliseOptional(detail.ImageUrl);
        enrichment.Link ??= NormaliseOptional(detail.Link);

        if (!string.IsNullOrWhiteSpace(detail.Name))
        {
            enrichment.ResolvedName = detail.Name;
        }

        return enrichment;
    }

    private async Task<EntityDbo> ResolveEntityForCreateAsync(
        string ownerId,
        ListDbo list,
        string name,
        CreateEntryRequest request,
        EnrichmentFields enrichment,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var resolvedName = enrichment.ResolvedName ?? name;
        var identifierType = StringUtilities.NormalizeWhitespace(request.Identifier?.Type);
        var identifierValue = StringUtilities.NormalizeWhitespace(request.Identifier?.Value);
        var metadata = CloneMetadata(request.Metadata);
        ApplyEnrichmentMetadata(metadata, enrichment);

        var requestedEntityId = StringUtilities.NormalizeWhitespace(request.EntityId);
        if (!string.IsNullOrWhiteSpace(requestedEntityId))
        {
            var byId = await _entityRepository.GetByIdAsync(requestedEntityId, cancellationToken).ConfigureAwait(false);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(identifierType) && !string.IsNullOrWhiteSpace(identifierValue))
        {
            var existing = await _entityRepository
                .FindByIdentifierAsync(identifierType, identifierValue, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return existing;
            }

            var link = enrichment.Link
                       ?? BuildDefaultLink(identifierType, identifierValue);

            return await _entityRepository
                .CreateAsync(
                    new EntityDbo
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = list.Configuration.EntityType,
                        Name = resolvedName,
                        Identifiers =
                        [
                            new EntityIdentifierDbo
                            {
                                Type = identifierType,
                                Value = identifierValue
                            }
                        ],
                        Metadata = metadata,
                        Link = link,
                        Added = now,
                        AddedByUserId = ownerId,
                        Edited = null,
                        ChangedByUserId = null
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await _entityRepository
            .CreateAsync(
                new EntityDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = list.Configuration.EntityType,
                    Name = resolvedName,
                    Identifiers = [],
                    Metadata = metadata,
                    Link = enrichment.Link,
                    Added = now,
                    AddedByUserId = ownerId,
                    Edited = null,
                    ChangedByUserId = null
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ListAccessContext> RequireReadableListAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken) =>
        await _listShareService.RequireAccessAsync(userId, listId, cancellationToken).ConfigureAwait(false);

    private async Task<ListAccessContext> RequireEditableListAsync(
        string userId,
        string listId,
        CancellationToken cancellationToken)
    {
        var access = await _listShareService
            .RequireAccessAsync(userId, listId, cancellationToken)
            .ConfigureAwait(false);

        if (!ListShareRoles.CanEditEntries(access.Role))
        {
            throw new UnauthorizedAccessException("You do not have permission to edit this list.");
        }

        return access;
    }

    private async Task<EntryDbo> RequireOwnedActiveEntryAsync(
        string ownerId,
        string listId,
        string entryId,
        CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(ownerId, entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null
            || !string.Equals(entry.ListId, listId, StringComparison.Ordinal)
            || entry.Deleted is not null)
        {
            throw new KeyNotFoundException("Entry was not found.");
        }

        return entry;
    }

    private static IEnumerable<EntryDbo> ApplySort(
        IEnumerable<EntryDbo> entries,
        string? sortBy,
        string? sortDirection)
    {
        var field = string.IsNullOrWhiteSpace(sortBy)
            ? EntrySortFields.Order
            : sortBy.Trim().ToLowerInvariant();
        var descending = string.Equals(sortDirection, ListSortDirections.Descending, StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<EntryDbo> ordered = field switch
        {
            EntrySortFields.Name => descending
                ? entries.OrderByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Author => descending
                ? entries.OrderByDescending(entry => entry.Author ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Author ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Director => descending
                ? entries.OrderByDescending(entry => entry.Director ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Director ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Actors => descending
                ? entries.OrderByDescending(entry => entry.Actors ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Actors ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Genre => descending
                ? entries.OrderByDescending(entry => entry.Genre ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Genre ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Themes => descending
                ? entries.OrderByDescending(entry => entry.Themes ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Themes ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            EntrySortFields.Added => descending
                ? entries.OrderByDescending(entry => entry.Added)
                : entries.OrderBy(entry => entry.Added),
            EntrySortFields.Read => descending
                ? entries.OrderByDescending(entry => entry.Read ?? false)
                : entries.OrderBy(entry => entry.Read ?? false),
            EntrySortFields.Visited => descending
                ? entries.OrderByDescending(entry => entry.Visited ?? false)
                : entries.OrderBy(entry => entry.Visited ?? false),
            EntrySortFields.Seen => descending
                ? entries.OrderByDescending(entry => entry.Seen ?? false)
                : entries.OrderBy(entry => entry.Seen ?? false),
            EntrySortFields.SeenAt => descending
                ? entries.OrderByDescending(entry => entry.SeenAt ?? DateTime.MinValue)
                : entries.OrderBy(entry => entry.SeenAt ?? DateTime.MinValue),
            _ => descending
                ? entries.OrderByDescending(entry => entry.Order)
                : entries.OrderBy(entry => entry.Order)
        };

        return field == EntrySortFields.Order ? ordered : ordered.ThenBy(entry => entry.Order);
    }

    private static bool Contains(string? source, string? term) =>
        !string.IsNullOrWhiteSpace(source)
        && !string.IsNullOrWhiteSpace(term)
        && source.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? BuildDefaultLink(string identifierType, string identifierValue)
    {
        if (string.Equals(identifierType, EntityIdentifierTypes.ImdbId, StringComparison.OrdinalIgnoreCase))
        {
            return $"https://www.imdb.com/title/{identifierValue}/";
        }

        if (string.Equals(identifierType, EntityIdentifierTypes.Isbn, StringComparison.OrdinalIgnoreCase))
        {
            return $"https://openlibrary.org/isbn/{identifierValue}";
        }

        if (string.Equals(identifierType, EntityIdentifierTypes.OpenLibraryWork, StringComparison.OrdinalIgnoreCase))
        {
            return $"https://openlibrary.org/works/{identifierValue}";
        }

        if (string.Equals(identifierType, EntityIdentifierTypes.Url, StringComparison.OrdinalIgnoreCase))
        {
            return identifierValue;
        }

        if (string.Equals(identifierType, EntityIdentifierTypes.Iso2, StringComparison.OrdinalIgnoreCase))
        {
            return $"https://flagdex.net/flags/{identifierValue.Trim().ToLowerInvariant()}.svg";
        }

        return null;
    }

    private static string? NormaliseOptional(string? value)
    {
        var normalised = StringUtilities.NormalizeWhitespace(value);
        return string.IsNullOrWhiteSpace(normalised) ? null : normalised;
    }

    private static string? GetMetadata(Dictionary<string, string>? metadata, string key)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static void ApplyEnrichmentMetadata(Dictionary<string, string> metadata, EnrichmentFields enrichment)
    {
        SetMetadata(metadata, "author", enrichment.Author);
        SetMetadata(metadata, "director", enrichment.Director);
        SetMetadata(metadata, "actors", enrichment.Actors);
        SetMetadata(metadata, "genre", enrichment.Genre);
        SetMetadata(metadata, "themes", enrichment.Themes);
        SetMetadata(metadata, "year", enrichment.Year);
        SetMetadata(metadata, "imageUrl", enrichment.ImageUrl);
    }

    private static void SetMetadata(Dictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static Dictionary<string, string> CloneMetadata(Dictionary<string, string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonElement> CloneAttributes(Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.Ordinal);
    }

    private async Task<EntryDto> MapToDtoAsync(EntryDbo entry, CancellationToken cancellationToken)
    {
        var dto = MapToDto(entry);
        if (string.IsNullOrWhiteSpace(entry.EntityId))
        {
            return dto;
        }

        var needsIdentifier = string.IsNullOrWhiteSpace(dto.IdentifierValue);
        var needsImage = string.IsNullOrWhiteSpace(dto.ImageUrl);
        if (!needsIdentifier && !needsImage)
        {
            return dto;
        }

        var entity = await _entityRepository.GetByIdAsync(entry.EntityId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return dto;
        }

        if (needsIdentifier)
        {
            var identifier = entity.Identifiers.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Type) && !string.IsNullOrWhiteSpace(item.Value));
            if (identifier is not null)
            {
                dto.IdentifierType = identifier.Type;
                dto.IdentifierValue = identifier.Value;
            }
        }

        if (needsImage)
        {
            dto.ImageUrl = GetMetadata(entity.Metadata, "imageUrl");
        }

        return dto;
    }

    private static EntryDto MapToDto(EntryDbo entry) =>
        new()
        {
            Id = entry.Id,
            ListId = entry.ListId,
            EntityId = entry.EntityId,
            IdentifierType = entry.IdentifierType,
            IdentifierValue = entry.IdentifierValue,
            Name = entry.Name,
            Link = entry.Link,
            ImageUrl = entry.ImageUrl,
            Author = entry.Author,
            Director = entry.Director,
            Actors = entry.Actors,
            Genre = entry.Genre,
            Themes = entry.Themes,
            Read = entry.Read,
            Visited = entry.Visited,
            Seen = entry.Seen,
            SeenAt = entry.SeenAt,
            Order = entry.Order,
            Attributes = MapAttributesForDto(entry.Attributes),
            Added = entry.Added,
            AddedByUserId = entry.AddedByUserId,
            AddedByUsername = entry.AddedByUsername,
            AddedByDisplayName = entry.AddedByDisplayName,
            Edited = entry.Edited
        };

    private static Dictionary<string, object?> MapAttributesForDto(Dictionary<string, JsonElement>? source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source is null || source.Count == 0)
        {
            return result;
        }

        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            result[pair.Key] = pair.Value.ValueKind switch
            {
                JsonValueKind.String => pair.Value.GetString(),
                JsonValueKind.Number => pair.Value.TryGetInt64(out var longValue)
                    ? longValue
                    : pair.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => pair.Value.GetRawText()
            };
        }

        return result;
    }

    private static bool IsWatchableEntityType(string? entityType) =>
        string.Equals(entityType, EntityTypes.Film, StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, EntityTypes.Tv, StringComparison.OrdinalIgnoreCase);

    private static DateTime? ResolveSeenAt(
        bool? seen,
        DateTime? requestedSeenAt,
        DateTime? existingSeenAt,
        DateTime now)
    {
        if (seen != true)
        {
            return null;
        }

        return requestedSeenAt ?? existingSeenAt ?? now;
    }

    private sealed class EnrichmentFields
    {
        public string? ResolvedName { get; set; }

        public string? Author { get; set; }

        public string? Director { get; set; }

        public string? Actors { get; set; }

        public string? Genre { get; set; }

        public string? Themes { get; set; }

        public string? Year { get; set; }

        public string? ImageUrl { get; set; }

        public string? Link { get; set; }
    }
}
