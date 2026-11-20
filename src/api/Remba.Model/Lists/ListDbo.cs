using System.Text.Json.Serialization;

namespace Remba.Model.Lists;

public class ListDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = ListRecordTypes.List;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = ListVisibility.Private;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("configuration")]
    public ListConfigurationDbo Configuration { get; set; } = new();

    [JsonPropertyName("viewSettings")]
    public ListViewSettingsDbo ViewSettings { get; set; } = new();

    [JsonPropertyName("externalLinks")]
    public List<ExternalLinkDbo> ExternalLinks { get; set; } = [];

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("changedByUserId")]
    public string? ChangedByUserId { get; set; }
}

public class ListConfigurationDbo
{
    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("attributes")]
    public List<ListAttributeDefinitionDbo> Attributes { get; set; } = [];
}

public class ListAttributeDefinitionDbo
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "entry";
}

public class ListViewSettingsDbo
{
    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "manual";

    [JsonPropertyName("sortDirection")]
    public string SortDirection { get; set; } = ListSortDirections.Ascending;

    [JsonPropertyName("viewMode")]
    public string ViewMode { get; set; } = ListViewModes.List;

    [JsonPropertyName("visibleFields")]
    public List<string> VisibleFields { get; set; } = [];
}

public class ExternalLinkDbo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }
}
