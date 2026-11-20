using System.Text.Json.Serialization;

namespace Remba.Model.Entities;

public class EntityDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("identifiers")]
    public List<EntityIdentifierDbo> Identifiers { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("changedByUserId")]
    public string? ChangedByUserId { get; set; }
}

public class EntityIdentifierDbo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
