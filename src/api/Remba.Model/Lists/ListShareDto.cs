namespace Remba.Model.Lists;

public class ListShareMemberDto
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = ListShareRoles.Editor;

    public string? ShareLabel { get; set; }

    public DateTime Added { get; set; }
}

public class ShareListRequest
{
    /// <summary>
    /// One or more Remba usernames to share with (partner = one, group of friends = many).
    /// </summary>
    public List<string> Usernames { get; set; } = [];

    /// <summary>
    /// Optional existing share group to expand into members.
    /// </summary>
    public string? ShareGroupId { get; set; }

    /// <summary>
    /// editor, adder, or viewer. Defaults to editor.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Optional label for this sharing circle (e.g. "Us").
    /// </summary>
    public string? ShareLabel { get; set; }
}

public class UpdateListShareRoleRequest
{
    /// <summary>
    /// editor, adder, or viewer.
    /// </summary>
    public string Role { get; set; } = ListShareRoles.Editor;
}

public class ShareGroupDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ShareGroupMemberDto> Members { get; set; } = [];

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class ShareGroupMemberDto
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public class CreateShareGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public List<string> Usernames { get; set; } = [];
}

public class UpdateShareGroupRequest
{
    public string? Name { get; set; }

    public List<string>? Usernames { get; set; }
}

public class ListAccessContext
{
    public required ListDbo List { get; init; }

    public required string Role { get; init; }

    public bool IsOwner => string.Equals(Role, ListShareRoles.Owner, StringComparison.Ordinal);
}
