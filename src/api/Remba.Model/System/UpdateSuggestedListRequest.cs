using System.ComponentModel.DataAnnotations;
using Remba.Model.Lists;

namespace Remba.Model.System;

public class UpdateSuggestedListRequest
{
    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? SortOrder { get; set; }

    public bool? Enabled { get; set; }

    [MaxLength(80)]
    public string? EntityType { get; set; }

    public SuggestedListIdentifierDbo? Identifier { get; set; }

    public SuggestedListEnrichmentDbo? Enrichment { get; set; }

    public List<ListAttributeDefinitionDbo>? DefaultEntryAttributes { get; set; }
}
