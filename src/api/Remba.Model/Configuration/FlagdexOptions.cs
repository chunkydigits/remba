namespace Remba.Model.Configuration;

public class FlagdexOptions
{
    public const string SectionName = "Flagdex";

    public string BaseUrl { get; set; } = "https://flagdex.net/";

    /// <summary>
    /// Absolute base used when Flagdex returns relative flag paths.
    /// </summary>
    public string FlagBaseUrl { get; set; } = "https://flagdex.net";
}
