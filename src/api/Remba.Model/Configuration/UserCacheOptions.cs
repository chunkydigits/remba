namespace Remba.Model.Configuration;

public class UserCacheOptions
{
    public const string SectionName = "UserCache";

    /// <summary>
    /// How long Auth0 subject → Remba user mappings stay in memory.
    /// </summary>
    public int AbsoluteExpirationSeconds { get; set; } = 300;
}
