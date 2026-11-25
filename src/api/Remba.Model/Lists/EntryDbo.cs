using System.Text.Json;
using System.Text.Json.Serialization;
using Remba.Model.Entities;

namespace Remba.Model.Lists;

public class EntryDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = ListRecordTypes.Entry;

    [JsonPropertyName("listId")]
    public string ListId { get; set; } = string.Empty;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("entityId")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("identifierType")]
    public string? IdentifierType { get; set; }

    [JsonPropertyName("identifierValue")]
    public string? IdentifierValue { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("director")]
    public string? Director { get; set; }

    [JsonPropertyName("actors")]
    public string? Actors { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("themes")]
    public string? Themes { get; set; }

    /// <summary>
    /// User relationship flag (books). Null when not applicable.
    /// </summary>
    [JsonPropertyName("read")]
    public bool? Read { get; set; }

    /// <summary>
    /// User relationship flag (countries). Null when not applicable.
    /// </summary>
    [JsonPropertyName("visited")]
    public bool? Visited { get; set; }

    /// <summary>
    /// User relationship flag (films / TV). Null when not applicable.
    /// </summary>
    [JsonPropertyName("seen")]
    public bool? Seen { get; set; }

    /// <summary>
    /// When the user marked the film / TV item as seen.
    /// </summary>
    [JsonPropertyName("seenAt")]
    public DateTime? SeenAt { get; set; }

    /// <summary>
    /// Cached streaming platforms for this title (denormalised from the catalog entity).
    /// </summary>
    [JsonPropertyName("watchProviders")]
    public List<WatchProviderInfo> WatchProviders { get; set; } = [];

    [JsonPropertyName("watchProvidersRegion")]
    public string? WatchProvidersRegion { get; set; }

    [JsonPropertyName("watchProvidersLink")]
    public string? WatchProvidersLink { get; set; }

    [JsonPropertyName("watchProvidersFetchedAt")]
    public DateTime? WatchProvidersFetchedAt { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    /// <summary>
    /// Remba user who added this entry to the list (may differ from list owner on shared lists).
    /// </summary>
    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("addedByUsername")]
    public string? AddedByUsername { get; set; }

    [JsonPropertyName("addedByDisplayName")]
    public string? AddedByDisplayName { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("deleted")]
    public DateTime? Deleted { get; set; }

    [JsonPropertyName("deletedByUserId")]
    public string? DeletedByUserId { get; set; }
}
