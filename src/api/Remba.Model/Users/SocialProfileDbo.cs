using System.Text.Json.Serialization;

namespace Remba.Model.Users;

public class SocialProfileDbo
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
