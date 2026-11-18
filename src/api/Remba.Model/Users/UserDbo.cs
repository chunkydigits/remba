using System.Text.Json.Serialization;

namespace Remba.Model.Users;

public class UserDbo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("auth0Subject")]
    public string Auth0Subject { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public UserProfileDbo Profile { get; set; } = new();

    [JsonPropertyName("socialProfiles")]
    public List<SocialProfileDbo> SocialProfiles { get; set; } = [];

    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }
}
