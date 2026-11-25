using System.Text.Json.Serialization;

namespace Remba.Model.Entities;

/// <summary>
/// Where a film/TV title can be watched in a given territory (from TMDB / JustWatch).
/// </summary>
public class WatchProviderInfo
{
    [JsonPropertyName("providerId")]
    public int ProviderId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Absolute logo URL (TMDB image CDN).
    /// </summary>
    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// flatrate (subscription), free, ads, rent, or buy.
    /// </summary>
    [JsonPropertyName("offerType")]
    public string OfferType { get; set; } = "flatrate";

    [JsonPropertyName("displayPriority")]
    public int DisplayPriority { get; set; }
}

public class WatchProvidersSnapshot
{
    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("providers")]
    public List<WatchProviderInfo> Providers { get; set; } = [];

    /// <summary>
    /// TMDB watch page for this title/region (deep links / JustWatch attribution).
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }
}
