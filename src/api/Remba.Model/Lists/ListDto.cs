namespace Remba.Model.Lists;

public class ListDto
{
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string Visibility { get; set; } = ListVisibility.Private;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public ListViewSettingsDto ViewSettings { get; set; } = new();

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class ListViewSettingsDto
{
    public string SortBy { get; set; } = "manual";

    public string SortDirection { get; set; } = ListSortDirections.Ascending;

    public string ViewMode { get; set; } = ListViewModes.List;

    public List<string> VisibleFields { get; set; } = [];
}

public class ListSummaryDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Visibility { get; set; } = ListVisibility.Private;

    public string? EntityType { get; set; }

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}
