using Remba.Model.Lists;

namespace Remba.Model.System;

public class SuggestedListDto
{
    public string SeedKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool Enabled { get; set; }

    public string? EntityType { get; set; }

    public SuggestedListIdentifierDto Identifier { get; set; } = new();

    public SuggestedListEnrichmentDto Enrichment { get; set; } = new();

    public List<ListAttributeDefinitionDto> DefaultEntryAttributes { get; set; } = [];

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class SuggestedListIdentifierDto
{
    public string Type { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string UrlTemplate { get; set; } = string.Empty;
}

public class SuggestedListEnrichmentDto
{
    public string Provider { get; set; } = string.Empty;

    public List<SuggestedListEnrichmentAttributeDto> AvailableAttributes { get; set; } = [];

    public List<string> DefaultSelectedAttributeKeys { get; set; } = [];
}

public class SuggestedListEnrichmentAttributeDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "text";
}

public class ListAttributeDefinitionDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "text";

    public string Scope { get; set; } = "entry";
}
