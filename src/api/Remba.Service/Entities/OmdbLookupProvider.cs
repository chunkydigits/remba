using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public partial class OmdbLookupProvider : IEntityLookupProvider
{
    private readonly HttpClient _httpClient;
    private readonly OmdbOptions _options;

    public OmdbLookupProvider(
        HttpClient httpClient,
        IOptions<OmdbOptions> options,
        ILogger<OmdbLookupProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderKey => EntityLookupProviders.Imdb;

    public bool SupportsEntityType(string entityType)
    {
        var normalised = entityType.Trim().ToLowerInvariant();
        return normalised is EntityTypes.Film or EntityTypes.Tv;
    }

    public async Task<IReadOnlyList<EntitySearchResultDto>> SearchAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "OMDb is not configured. Set Omdb:ApiKey to enable film/TV search.");
        }

        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedQuery))
        {
            return [];
        }

        var omdbType = string.Equals(entityType, EntityTypes.Tv, StringComparison.OrdinalIgnoreCase)
            ? "series"
            : "movie";

        if (ImdbIdPattern().IsMatch(normalisedQuery))
        {
            var byId = await GetByImdbIdAsync(normalisedQuery, omdbType, cancellationToken)
                .ConfigureAwait(false);
            return byId is null ? [] : [byId];
        }

        return await SearchByTitleAsync(normalisedQuery, omdbType, cancellationToken).ConfigureAwait(false);
    }

    public Task<EntitySearchResultDto?> GetByIdentifierAsync(
        string entityType,
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsEntityType(entityType)
            || !string.Equals(identifierType, EntityIdentifierTypes.ImdbId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<EntitySearchResultDto?>(null);
        }

        var omdbType = string.Equals(entityType, EntityTypes.Tv, StringComparison.OrdinalIgnoreCase)
            ? "series"
            : "movie";

        return GetByImdbIdAsync(identifierValue.Trim(), omdbType, cancellationToken);
    }

    private async Task<EntitySearchResultDto?> GetByImdbIdAsync(
        string imdbId,
        string omdbType,
        CancellationToken cancellationToken)
    {
        var url = $"?apikey={Uri.EscapeDataString(_options.ApiKey)}&i={Uri.EscapeDataString(imdbId)}&type={omdbType}";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OmdbDetailResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || !IsSuccess(payload.Response) || string.IsNullOrWhiteSpace(payload.ImdbId))
        {
            return null;
        }

        return MapDetail(payload);
    }

    private async Task<IReadOnlyList<EntitySearchResultDto>> SearchByTitleAsync(
        string title,
        string omdbType,
        CancellationToken cancellationToken)
    {
        var url =
            $"?apikey={Uri.EscapeDataString(_options.ApiKey)}&s={Uri.EscapeDataString(title)}&type={omdbType}";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<OmdbSearchResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || !IsSuccess(payload.Response) || payload.Search is null || payload.Search.Count == 0)
        {
            return [];
        }

        return payload.Search
            .Where(item => !string.IsNullOrWhiteSpace(item.ImdbId) && !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new EntitySearchResultDto
            {
                Provider = ProviderKey,
                IdentifierType = EntityIdentifierTypes.ImdbId,
                IdentifierValue = item.ImdbId!,
                Name = item.Title!,
                Year = NullIfNaa(item.Year),
                Subtitle = NullIfNaa(item.Type),
                ImageUrl = NullIfNaa(item.Poster),
                Link = $"https://www.imdb.com/title/{item.ImdbId}/"
            })
            .Take(10)
            .ToList();
    }

    private static EntitySearchResultDto MapDetail(OmdbDetailResponse payload) =>
        new()
        {
            Provider = EntityLookupProviders.Imdb,
            IdentifierType = EntityIdentifierTypes.ImdbId,
            IdentifierValue = payload.ImdbId!,
            Name = payload.Title ?? payload.ImdbId!,
            Year = NullIfNaa(payload.Year),
            Subtitle = NullIfNaa(payload.Director) ?? NullIfNaa(payload.Type),
            Director = NullIfNaa(payload.Director),
            Actors = NullIfNaa(payload.Actors),
            ImageUrl = NullIfNaa(payload.Poster),
            Link = $"https://www.imdb.com/title/{payload.ImdbId}/"
        };

    private static bool IsSuccess(string? response) =>
        string.Equals(response, "True", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfNaa(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();

    [GeneratedRegex(@"^tt\d{7,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdPattern();

    private sealed class OmdbSearchResponse
    {
        [JsonPropertyName("Search")]
        public List<OmdbSearchItem>? Search { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }
    }

    private sealed class OmdbSearchItem
    {
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Year")]
        public string? Year { get; set; }

        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Poster")]
        public string? Poster { get; set; }
    }

    private sealed class OmdbDetailResponse
    {
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Year")]
        public string? Year { get; set; }

        [JsonPropertyName("Director")]
        public string? Director { get; set; }

        [JsonPropertyName("Actors")]
        public string? Actors { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Poster")]
        public string? Poster { get; set; }

        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }
    }
}
