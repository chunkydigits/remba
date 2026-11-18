using System.ComponentModel.DataAnnotations;

namespace Remba.Model.Users;

public class UpdateUserRequest
{
    [MaxLength(80)]
    public string? DisplayName { get; set; }

    [MaxLength(40)]
    public string? Username { get; set; }

    public UpdateUserProfileRequest? Profile { get; set; }

    public List<UpdateSocialProfileRequest>? SocialProfiles { get; set; }
}

public class UpdateUserProfileRequest
{
    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(2048)]
    [Url]
    public string? Image { get; set; }

    public string? Visibility { get; set; }
}

public class UpdateSocialProfileRequest
{
    [Required]
    [MaxLength(40)]
    public string Platform { get; set; } = string.Empty;

    [Required]
    [MaxLength(2048)]
    [Url]
    public string Url { get; set; } = string.Empty;
}
