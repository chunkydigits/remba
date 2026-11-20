using System.ComponentModel.DataAnnotations;

namespace Remba.Model.Lists;

public class CreateListRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

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

    [MaxLength(40)]
    public string? EntityType { get; set; }

    [MaxLength(20)]
    public string? Visibility { get; set; }
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
