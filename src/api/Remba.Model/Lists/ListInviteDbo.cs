using System.Text.Json.Serialization;

namespace Remba.Model.Lists;

/// <summary>
/// Single-use invite code for joining a shared list. Lives in the list owner's partition.
/// Once redeemed, <see cref="UsedAt"/> is set and the code cannot be used again.
/// </summary>
public class ListInviteDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = ListRecordTypes.ListInvite;

    /// <summary>
    /// Cosmos partition key (list owner).
    /// </summary>
    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("listId")]
    public string ListId { get; set; } = string.Empty;

    [JsonPropertyName("listOwnerId")]
    public string ListOwnerId { get; set; } = string.Empty;

    [JsonPropertyName("listName")]
    public string ListName { get; set; } = string.Empty;

    /// <summary>
    /// Human-friendly single-use code (e.g. ABCD-EFGH).
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = ListShareRoles.Editor;

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("usedAt")]
    public DateTime? UsedAt { get; set; }

    [JsonPropertyName("usedByUserId")]
    public string? UsedByUserId { get; set; }

    [JsonPropertyName("usedByUsername")]
    public string? UsedByUsername { get; set; }

    [JsonPropertyName("usedByDisplayName")]
    public string? UsedByDisplayName { get; set; }
}

public class ListInviteDto
{
    public string Id { get; set; } = string.Empty;

    public string ListId { get; set; } = string.Empty;

    public string ListName { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Role { get; set; } = ListShareRoles.Editor;

    public DateTime Added { get; set; }

    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    public string? UsedByUserId { get; set; }

    public string? UsedByUsername { get; set; }

    public string? UsedByDisplayName { get; set; }
}

public class CreateListInviteRequest
{
    /// <summary>
    /// editor or viewer. Defaults to editor.
    /// </summary>
    public string? Role { get; set; }
}

public class RedeemListInviteRequest
{
    public string Code { get; set; } = string.Empty;
}

public class RedeemListInviteResponse
{
    public string ListId { get; set; } = string.Empty;

    public string ListName { get; set; } = string.Empty;

    public string Role { get; set; } = ListShareRoles.Editor;
}
