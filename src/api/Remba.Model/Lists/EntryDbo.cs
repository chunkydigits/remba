using System.Text.Json;
using System.Text.Json.Serialization;

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

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("director")]
    public string? Director { get; set; }

    [JsonPropertyName("actors")]
    public string? Actors { get; set; }

    /// <summary>
    /// User relationship flag (books). Null when not applicable.
    /// </summary>
    [JsonPropertyName("read")]
    public bool? Read { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("deleted")]
    public DateTime? Deleted { get; set; }

    [JsonPropertyName("deletedByUserId")]
    public string? DeletedByUserId { get; set; }
}
