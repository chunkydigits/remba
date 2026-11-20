using System.Text.Json.Serialization;
using Remba.Model.Lists;

namespace Remba.Model.System;

public class SuggestedListDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = SystemRecordTypes.SuggestedList;

    [JsonPropertyName("seedKey")]
    public string SeedKey { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("identifier")]
    public SuggestedListIdentifierDbo Identifier { get; set; } = new();

    [JsonPropertyName("enrichment")]
    public SuggestedListEnrichmentDbo Enrichment { get; set; } = new();

    [JsonPropertyName("defaultEntryAttributes")]
    public List<ListAttributeDefinitionDbo> DefaultEntryAttributes { get; set; } = [];

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }
}

public class SuggestedListIdentifierDbo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("urlTemplate")]
    public string UrlTemplate { get; set; } = string.Empty;
}

public class SuggestedListEnrichmentDbo
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("availableAttributes")]
    public List<SuggestedListEnrichmentAttributeDbo> AvailableAttributes { get; set; } = [];

    [JsonPropertyName("defaultSelectedAttributeKeys")]
    public List<string> DefaultSelectedAttributeKeys { get; set; } = [];
}

public class SuggestedListEnrichmentAttributeDbo
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
}
