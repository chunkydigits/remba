using System.Text.Json;
using Microsoft.Extensions.Logging;
using Remba.Model.Entities;
using Remba.Model.Lists;
using Remba.Repository.Entities;
using Remba.Repository.Lists;
using Remba.Service.Entities;
using Remba.Utility;

namespace Remba.Service.Lists;

public class EntryService : IEntryService
{
    private readonly IListRepository _listRepository;
    private readonly IEntryRepository _entryRepository;
    private readonly IEntityRepository _entityRepository;
    private readonly IEntityLookupService _entityLookupService;
    private readonly ILogger<EntryService> _logger;

    public EntryService(
        IListRepository listRepository,
        IEntryRepository entryRepository,
        IEntityRepository entityRepository,
        IEntityLookupService entityLookupService,
        ILogger<EntryService> logger)
    {
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
        _entryRepository = entryRepository ?? throw new ArgumentNullException(nameof(entryRepository));
        _entityRepository = entityRepository ?? throw new ArgumentNullException(nameof(entityRepository));
        _entityLookupService = entityLookupService ?? throw new ArgumentNullException(nameof(entityLookupService));
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

        var name = StringUtilities.NormalizeWhitespace(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Entry name is required.");
        }

        var list = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var enrichment = await ResolveEnrichmentAsync(list, request, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var entity = await ResolveEntityForCreateAsync(
                ownerId,
                list,
                name,
                request,
                enrichment,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var existingEntry = await _entryRepository
            .FindActiveByEntityAsync(ownerId, listId, entity.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existingEntry is not null)
        {
            throw new InvalidOperationException("That item is already on this list.");
        }

        var order = await _entryRepository
            .GetNextOrderAsync(ownerId, listId, cancellationToken)
            .ConfigureAwait(false);

        var isBook = string.Equals(list.Configuration.EntityType, EntityTypes.Book, StringComparison.OrdinalIgnoreCase);
        var identifierType = StringUtilities.NormalizeWhitespace(request.Identifier?.Type)
            ?? entity.Identifiers.FirstOrDefault()?.Type;
        var identifierValue = StringUtilities.NormalizeWhitespace(request.Identifier?.Value)
            ?? entity.Identifiers.FirstOrDefault()?.Value;

        var entry = await _entryRepository
            .CreateAsync(
                new EntryDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RecordType = ListRecordTypes.Entry,
                    ListId = listId,
                    OwnerId = ownerId,
                    EntityId = entity.Id,
                    IdentifierType = NormaliseOptional(identifierType),
                    IdentifierValue = NormaliseOptional(identifierValue),
                    Name = entity.Name,
                    Link = entity.Link ?? NormaliseOptional(request.Link),
                    Author = enrichment.Author,
                    Director = enrichment.Director,
                    Actors = enrichment.Actors,
                    Read = isBook ? request.Read ?? false : request.Read,
                    Order = order,
                    Attributes = CloneAttributes(request.Attributes),
                    Added = now,
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
        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var entries = await _entryRepository
            .GetByListAsync(ownerId, listId, includeDeleted: false, cancellationToken)
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

            if (query.Read is not null)
            {
                filtered = filtered.Where(entry => entry.Read == query.Read);
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
        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var entry = await _entryRepository.GetByIdAsync(ownerId, entryId, cancellationToken).ConfigureAwait(false);
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

        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var entry = await RequireOwnedActiveEntryAsync(ownerId, listId, entryId, cancellationToken)
            .ConfigureAwait(false);

        if (request.Name is not null)
        {
            var name = StringUtilities.NormalizeWhitespace(request.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Entry name cannot be empty.");
            }

            entry.Name = name;

            var entity = await _entityRepository.GetByIdAsync(entry.EntityId, cancellationToken).ConfigureAwait(false);
            if (entity is not null)
            {
                entity.Name = name;
                entity.Edited = DateTime.UtcNow;
                entity.ChangedByUserId = ownerId;
                await _entityRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }

        if (request.Link is not null)
        {
            entry.Link = NormaliseOptional(request.Link);
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

        if (request.Read is not null)
        {
            entry.Read = request.Read;
        }

        if (request.Attributes is not null)
        {
            entry.Attributes = CloneAttributes(request.Attributes);
        }

        entry.Edited = DateTime.UtcNow;

        var updated = await _entryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        return MapToDto(updated);
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

        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var existing = await _entryRepository
            .GetByListAsync(ownerId, listId, includeDeleted: false, cancellationToken)
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
        _ = await RequireOwnedListAsync(ownerId, listId, cancellationToken).ConfigureAwait(false);
        var entry = await RequireOwnedActiveEntryAsync(ownerId, listId, entryId, cancellationToken)
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
            Year = NormaliseOptional(GetMetadata(request.Metadata, "year")),
            ImageUrl = NormaliseOptional(GetMetadata(request.Metadata, "imageUrl")),
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
            EntrySortFields.Added => descending
                ? entries.OrderByDescending(entry => entry.Added)
                : entries.OrderBy(entry => entry.Added),
            EntrySortFields.Read => descending
                ? entries.OrderByDescending(entry => entry.Read ?? false)
                : entries.OrderBy(entry => entry.Read ?? false),
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
        if (!string.IsNullOrWhiteSpace(dto.IdentifierValue) || string.IsNullOrWhiteSpace(entry.EntityId))
        {
            return dto;
        }

        var entity = await _entityRepository.GetByIdAsync(entry.EntityId, cancellationToken).ConfigureAwait(false);
        var identifier = entity?.Identifiers.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Type) && !string.IsNullOrWhiteSpace(item.Value));
        if (identifier is null)
        {
            return dto;
        }

        dto.IdentifierType = identifier.Type;
        dto.IdentifierValue = identifier.Value;
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
            Author = entry.Author,
            Director = entry.Director,
            Actors = entry.Actors,
            Read = entry.Read,
            Order = entry.Order,
            Attributes = new Dictionary<string, JsonElement>(entry.Attributes, StringComparer.Ordinal),
            Added = entry.Added,
            Edited = entry.Edited
        };

    private sealed class EnrichmentFields
    {
        public string? ResolvedName { get; set; }

        public string? Author { get; set; }

        public string? Director { get; set; }

        public string? Actors { get; set; }

        public string? Year { get; set; }

        public string? ImageUrl { get; set; }

        public string? Link { get; set; }
    }
}
