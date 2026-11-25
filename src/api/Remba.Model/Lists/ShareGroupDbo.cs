using System.Text.Json.Serialization;

namespace Remba.Model.Lists;

/// <summary>
/// Named set of people (partner or friends) that the owner can share lists with.
/// Lives in the creator's <c>user-lists</c> partition.
/// </summary>
public class ShareGroupDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("recordType")]
    public string RecordType { get; set; } = ListRecordTypes.ShareGroup;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ShareGroupMemberDbo> Members { get; set; } = [];

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("addedByUserId")]
    public string AddedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("changedByUserId")]
    public string? ChangedByUserId { get; set; }
}

public class ShareGroupMemberDbo
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
