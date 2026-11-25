namespace Remba.Model.Configuration;

public class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    /// <summary>
    /// ISO 3166-1 alpha-2 region used for watch-provider lookups (e.g. GB, US).
    /// </summary>
    public string DefaultRegion { get; set; } = "GB";

    public string LogoBaseUrl { get; set; } = "https://image.tmdb.org/t/p/w92";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
