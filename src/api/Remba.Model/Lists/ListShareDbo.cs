using System.Text.Json.Serialization;

namespace Remba.Model.Lists;

/// <summary>
/// Dual-written share grant: one copy in the list owner's partition (manage members)
/// and one in the member's partition (lists shared with me + access resolution).
/// Partition key is <see cref="OwnerId"/> (the partition this copy lives in).
/// </summary>
public class ListShareDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = ListRecordTypes.ListShare;

    /// <summary>
    /// Cosmos partition key for this copy (list owner or member).
    /// </summary>
    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("listId")]
    public string ListId { get; set; } = string.Empty;

    [JsonPropertyName("listOwnerId")]
    public string ListOwnerId { get; set; } = string.Empty;

    [JsonPropertyName("listName")]
    public string ListName { get; set; } = string.Empty;

    [JsonPropertyName("memberUserId")]
    public string MemberUserId { get; set; } = string.Empty;

    [JsonPropertyName("memberUsername")]
    public string MemberUsername { get; set; } = string.Empty;

    [JsonPropertyName("memberDisplayName")]
    public string MemberDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = ListShareRoles.Editor;

    /// <summary>
    /// Optional label for the sharing circle (e.g. "Us", "Weekend crew").
    /// </summary>
    [JsonPropertyName("shareLabel")]
    public string? ShareLabel { get; set; }

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;
}

public static class ListShareRoles
{
    public const string Owner = "owner";

    public const string Editor = "editor";

    public const string Viewer = "viewer";

    public static bool IsValidMemberRole(string role) =>
        role is Editor or Viewer;

    public static bool CanEditEntries(string role) =>
        role is Owner or Editor;

    public static bool CanManageShares(string role) =>
        role is Owner;

    public static bool CanManageList(string role) =>
        role is Owner;
}
