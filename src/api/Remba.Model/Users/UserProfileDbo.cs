using System.Text.Json.Serialization;

namespace Remba.Model.Users;

public class UserProfileDbo
{
    [JsonPropertyName("bio")]
    public string Bio { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = ProfileVisibility.Private;
}

public static class ProfileVisibility
{
    public const string Private = "private";
    public const string Public = "public";
}
