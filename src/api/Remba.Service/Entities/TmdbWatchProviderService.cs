using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public class TmdbWatchProviderService : ITmdbWatchProviderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;
    private readonly ILogger<TmdbWatchProviderService> _logger;

    public TmdbWatchProviderService(
        HttpClient httpClient,
        IOptions<TmdbOptions> options,
        ILogger<TmdbWatchProviderService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured => _options.IsConfigured;

    public string DefaultRegion =>
        string.IsNullOrWhiteSpace(_options.DefaultRegion) ? "GB" : _options.DefaultRegion.Trim().ToUpperInvariant();

    public async Task<WatchProvidersSnapshot?> GetByImdbIdAsync(
        string entityType,
        string imdbId,
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "TMDB watch-provider lookup skipped: Tmdb:ApiKey is not configured.");
            return null;
        }

        var normalisedImdb = StringUtilities.NormalizeWhitespace(imdbId);
        if (string.IsNullOrWhiteSpace(normalisedImdb))
        {
            _logger.LogWarning("TMDB watch-provider lookup skipped: IMDb id was empty.");
            return null;
        }

        var territory = string.IsNullOrWhiteSpace(region)
            ? DefaultRegion
            : region.Trim().ToUpperInvariant();

        _logger.LogInformation(
            "TMDB watch-provider lookup starting for IMDb {ImdbId} (entityType={EntityType}, region={Region})",
            normalisedImdb,
            entityType,
            territory);

        try
        {
            var tmdbId = await ResolveTmdbIdAsync(entityType, normalisedImdb, cancellationToken)
                .ConfigureAwait(false);
            if (tmdbId is null)
            {
                _logger.LogWarning(
                    "TMDB find returned no movie/TV match for IMDb id {ImdbId} (entityType={EntityType})",
                    normalisedImdb,
                    entityType);
                return null;
            }

            _logger.LogInformation(
                "TMDB resolved IMDb {ImdbId} to {MediaType}/{TmdbId}",
                normalisedImdb,
                tmdbId.MediaType,
                tmdbId.Id);

            var path = tmdbId.MediaType == "tv"
                ? $"tv/{tmdbId.Id}/watch/providers"
                : $"movie/{tmdbId.Id}/watch/providers";

            var (response, rawBody) = await GetJsonAsync<TmdbWatchProvidersResponse>(path, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Results is null || response.Results.Count == 0)
            {
                _logger.LogWarning(
                    "TMDB watch/providers returned no country results for {MediaType}/{TmdbId}. Body={Body}",
                    tmdbId.MediaType,
                    tmdbId.Id,
                    Truncate(rawBody, 500));
                return new WatchProvidersSnapshot
                {
                    Region = territory,
                    Providers = [],
                    Link = null,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var availableRegions = string.Join(", ", response.Results.Keys.OrderBy(key => key, StringComparer.Ordinal));
            if (!response.Results.TryGetValue(territory, out var country) || country is null)
            {
                _logger.LogWarning(
                    "TMDB watch/providers has no entry for region {Region} on {MediaType}/{TmdbId}. Available regions: {Regions}",
                    territory,
                    tmdbId.MediaType,
                    tmdbId.Id,
                    availableRegions);
                return new WatchProvidersSnapshot
                {
                    Region = territory,
                    Providers = [],
                    Link = null,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var providers = MapProviders(country)
                .OrderBy(provider => provider.DisplayPriority)
                .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogInformation(
                "TMDB/JustWatch providers for IMDb {ImdbId} in {Region}: count={Count}, names=[{Names}], link={Link}, flatrate={Flatrate}, free={Free}, ads={Ads}",
                normalisedImdb,
                territory,
                providers.Count,
                string.Join(", ", providers.Select(provider => $"{provider.Name}({provider.OfferType})")),
                country.Link,
                country.Flatrate?.Count ?? 0,
                country.Free?.Count ?? 0,
                country.Ads?.Count ?? 0);

            return new WatchProvidersSnapshot
            {
                Region = territory,
                Providers = providers,
                Link = string.IsNullOrWhiteSpace(country.Link) ? null : country.Link.Trim(),
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or global::System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "TMDB watch-provider lookup failed for IMDb id {ImdbId}", normalisedImdb);
            return null;
        }
    }

    private async Task<TmdbIdMatch?> ResolveTmdbIdAsync(
        string entityType,
        string imdbId,
        CancellationToken cancellationToken)
    {
        var path = $"find/{Uri.EscapeDataString(imdbId)}?external_source=imdb_id";
        var (find, rawBody) = await GetJsonAsync<TmdbFindResponse>(path, cancellationToken).ConfigureAwait(false);

        if (find is null)
        {
            _logger.LogWarning(
                "TMDB find returned empty/unparseable body for IMDb {ImdbId}. Body={Body}",
                imdbId,
                Truncate(rawBody, 500));
            return null;
        }

        var movieCount = find.MovieResults?.Count ?? 0;
        var tvCount = find.TvResults?.Count ?? 0;
        _logger.LogInformation(
            "TMDB find for IMDb {ImdbId}: movie_results={MovieCount}, tv_results={TvCount}",
            imdbId,
            movieCount,
            tvCount);

        var prefersTv = string.Equals(entityType, EntityTypes.Tv, StringComparison.OrdinalIgnoreCase);
        if (prefersTv)
        {
            var tv = find.TvResults?.FirstOrDefault();
            if (tv is not null)
            {
                return new TmdbIdMatch(tv.Id, "tv");
            }
        }

        var movie = find.MovieResults?.FirstOrDefault();
        if (movie is not null)
        {
            return new TmdbIdMatch(movie.Id, "movie");
        }

        var series = find.TvResults?.FirstOrDefault();
        return series is null ? null : new TmdbIdMatch(series.Id, "tv");
    }

    private async Task<(TResponse? Value, string RawBody)> GetJsonAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var requestPath = AppendApiKey(relativePath);
        _logger.LogDebug("TMDB GET {Path}", RedactApiKey(requestPath));

        using var response = await _httpClient.GetAsync(requestPath, cancellationToken).ConfigureAwait(false);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "TMDB GET {Path} failed with {StatusCode}. Body={Body}",
                RedactApiKey(requestPath),
                (int)response.StatusCode,
                Truncate(rawBody, 500));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return (default, rawBody);
        }

        var value = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
        return (value, rawBody);
    }

    private IEnumerable<WatchProviderInfo> MapProviders(TmdbWatchCountry country)
    {
        foreach (var item in country.Flatrate ?? [])
        {
            yield return MapProvider(item, "flatrate");
        }

        foreach (var item in country.Free ?? [])
        {
            yield return MapProvider(item, "free");
        }

        foreach (var item in country.Ads ?? [])
        {
            yield return MapProvider(item, "ads");
        }
    }

    private WatchProviderInfo MapProvider(TmdbWatchProvider item, string offerType) =>
        new()
        {
            ProviderId = item.ProviderId,
            Name = item.ProviderName?.Trim() ?? $"Provider {item.ProviderId}",
            LogoUrl = BuildLogoUrl(item.LogoPath),
            OfferType = offerType,
            DisplayPriority = item.DisplayPriority
        };

    private string? BuildLogoUrl(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.LogoBaseUrl)
            ? "https://image.tmdb.org/t/p/w92"
            : _options.LogoBaseUrl.TrimEnd('/');
        var path = logoPath.StartsWith('/') ? logoPath : $"/{logoPath}";
        return $"{baseUrl}{path}";
    }

    private string AppendApiKey(string relativePath)
    {
        var separator = relativePath.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{relativePath.TrimStart('/')}{separator}api_key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    private static string RedactApiKey(string path)
    {
        const string marker = "api_key=";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return path;
        }

        var start = index + marker.Length;
        var end = path.IndexOf('&', start);
        if (end < 0)
        {
            end = path.Length;
        }

        return string.Concat(path.AsSpan(0, start), "***", path.AsSpan(end));
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private sealed record TmdbIdMatch(int Id, string MediaType);

    private sealed class TmdbFindResponse
    {
        [JsonPropertyName("movie_results")]
        public List<TmdbFindItem>? MovieResults { get; set; }

        [JsonPropertyName("tv_results")]
        public List<TmdbFindItem>? TvResults { get; set; }
    }

    private sealed class TmdbFindItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class TmdbWatchProvidersResponse
    {
        [JsonPropertyName("results")]
        public Dictionary<string, TmdbWatchCountry>? Results { get; set; }
    }

    private sealed class TmdbWatchCountry
    {
        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("flatrate")]
        public List<TmdbWatchProvider>? Flatrate { get; set; }

        [JsonPropertyName("free")]
        public List<TmdbWatchProvider>? Free { get; set; }

        [JsonPropertyName("ads")]
        public List<TmdbWatchProvider>? Ads { get; set; }
    }

    private sealed class TmdbWatchProvider
    {
        [JsonPropertyName("provider_id")]
        public int ProviderId { get; set; }

        [JsonPropertyName("provider_name")]
        public string? ProviderName { get; set; }

        [JsonPropertyName("logo_path")]
        public string? LogoPath { get; set; }

        [JsonPropertyName("display_priority")]
        public int DisplayPriority { get; set; }
    }
}
