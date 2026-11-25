namespace Remba.Model.Lists;

public class ListDto
{
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string Visibility { get; set; } = ListVisibility.Private;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ReferralLink { get; set; }

    public string? EntityType { get; set; }

    public List<ListAttributeDefinitionDto> Attributes { get; set; } = [];

    public ListViewSettingsDto ViewSettings { get; set; } = new();

    /// <summary>
    /// Caller's role on this list: owner, editor, adder, or viewer.
    /// </summary>
    public string MyRole { get; set; } = ListShareRoles.Owner;

    public bool IsOwned { get; set; } = true;

    public string? ShareLabel { get; set; }

    public List<ListShareMemberDto> Members { get; set; } = [];

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class ListAttributeDefinitionDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Type { get; set; } = ListAttributeTypes.Text;

    public string Scope { get; set; } = "entry";
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

    public string? ReferralLink { get; set; }

    public string MyRole { get; set; } = ListShareRoles.Owner;

    public bool IsOwned { get; set; } = true;

    public string? ShareLabel { get; set; }

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}
