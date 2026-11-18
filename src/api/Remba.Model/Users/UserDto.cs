namespace Remba.Model.Users;

public class UserDto
{
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserProfileDto Profile { get; set; } = new();

    public List<SocialProfileDto> SocialProfiles { get; set; } = [];

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class UserProfileDto
{
    public string Bio { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Visibility { get; set; } = ProfileVisibility.Private;
}

public class SocialProfileDto
{
    public string Platform { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
