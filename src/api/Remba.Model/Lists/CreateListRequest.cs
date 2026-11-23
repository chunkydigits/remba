using System.ComponentModel.DataAnnotations;

namespace Remba.Model.Lists;

public class CreateListRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? ReferralLink { get; set; }

    [MaxLength(40)]
    public string? EntityType { get; set; }

    [MaxLength(20)]
    public string? Visibility { get; set; }
}

public class UpdateListRequest
{
    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// When provided, replaces the collection referral link. Empty string clears it.
    /// </summary>
    [MaxLength(2000)]
    public string? ReferralLink { get; set; }

    [MaxLength(40)]
    public string? EntityType { get; set; }

    [MaxLength(20)]
    public string? Visibility { get; set; }

    /// <summary>
    /// When provided, replaces the list's custom entry properties.
    /// </summary>
    public List<ListAttributeDefinitionRequest>? Attributes { get; set; }
}

public class ListAttributeDefinitionRequest
{
    [MaxLength(80)]
    public string? Key { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = ListAttributeTypes.Text;
}

public class UpdateListViewSettingsRequest
{
    [MaxLength(40)]
    public string? SortBy { get; set; }

    [MaxLength(20)]
    public string? SortDirection { get; set; }

    [MaxLength(20)]
    public string? ViewMode { get; set; }

    public List<string>? VisibleFields { get; set; }
}
