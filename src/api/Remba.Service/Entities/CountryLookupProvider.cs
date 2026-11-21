using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public class CountryLookupProvider : IEntityLookupProvider
{
    private const string CacheKey = "flagdex:countries-lite";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly FlagdexOptions _options;
    private readonly ILogger<CountryLookupProvider> _logger;

    public CountryLookupProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<FlagdexOptions> options,
        ILogger<CountryLookupProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderKey => EntityLookupProviders.Flagdex;

    public bool SupportsEntityType(string entityType) =>
        string.Equals(entityType?.Trim(), EntityTypes.Country, StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<EntitySearchResultDto>> SearchAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsEntityType(entityType))
        {
            return [];
        }

        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedQuery))
        {
            return [];
        }

        var countries = await GetCountriesAsync(cancellationToken).ConfigureAwait(false);
        if (countries.Count == 0)
        {
            return [];
        }

        if (TryExactIsoMatch(countries, normalisedQuery, out var byCode))
        {
            return [Map(byCode)];
        }

        return countries
            .Where(country => MatchesName(country, normalisedQuery))
            .OrderBy(country => Rank(country, normalisedQuery))
            .ThenBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(Map)
            .ToList();
    }

    public async Task<EntitySearchResultDto?> GetByIdentifierAsync(
        string entityType,
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsEntityType(entityType)
            || !string.Equals(identifierType, EntityIdentifierTypes.Iso2, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!TryMatchIso(identifierValue, out var iso2) || iso2.Length != 2)
        {
            return null;
        }

        var countries = await GetCountriesAsync(cancellationToken).ConfigureAwait(false);
        var match = countries.FirstOrDefault(country =>
            string.Equals(country.Iso2, iso2, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : Map(match);
    }

    private async Task<IReadOnlyList<FlagdexCountry>> GetCountriesAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<FlagdexCountry>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await _httpClient
                .GetAsync("api/v1/countries-lite.json", cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadFromJsonAsync<List<FlagdexCountry>>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var countries = (payload ?? [])
                .Where(country => !string.IsNullOrWhiteSpace(country.Iso2) && !string.IsNullOrWhiteSpace(country.Name))
                .ToList();

            _cache.Set(CacheKey, (IReadOnlyList<FlagdexCountry>)countries, CacheDuration);
            return countries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Flagdex country list");
            throw new InvalidOperationException("Unable to load country directory from Flagdex.", ex);
        }
    }

    private EntitySearchResultDto Map(FlagdexCountry country)
    {
        var iso2 = country.Iso2.Trim().ToUpperInvariant();
        var capital = StringUtilities.NormalizeWhitespace(country.Capital);
        var region = StringUtilities.NormalizeWhitespace(country.Region);
        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(region))
        {
            subtitleParts.Add(region);
        }

        if (!string.IsNullOrWhiteSpace(capital))
        {
            subtitleParts.Add(capital);
        }

        return new EntitySearchResultDto
        {
            Provider = ProviderKey,
            IdentifierType = EntityIdentifierTypes.Iso2,
            IdentifierValue = iso2,
            Name = country.Name.Trim(),
            Subtitle = subtitleParts.Count == 0 ? iso2 : string.Join(" · ", subtitleParts),
            Genre = string.IsNullOrWhiteSpace(region) ? null : region,
            ImageUrl = BuildFlagUrl(iso2, country.Flag),
            Link = BuildWikipediaLink(country.Name)
        };
    }

    private string BuildFlagUrl(string iso2, string? relativeFlag)
    {
        if (!string.IsNullOrWhiteSpace(relativeFlag) && Uri.TryCreate(relativeFlag, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var flagBase = string.IsNullOrWhiteSpace(_options.FlagBaseUrl)
            ? "https://flagdex.net"
            : _options.FlagBaseUrl.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(relativeFlag) && relativeFlag.StartsWith('/'))
        {
            return $"{flagBase}{relativeFlag}";
        }

        return $"{flagBase}/flags/{iso2.ToLowerInvariant()}.svg";
    }

    private static string? BuildWikipediaLink(string name)
    {
        var trimmed = StringUtilities.NormalizeWhitespace(name);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var slug = trimmed.Replace(' ', '_');
        return $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(slug).Replace("%2F", "/", StringComparison.Ordinal)}";
    }

    private static bool MatchesName(FlagdexCountry country, string query)
    {
        if (country.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(country.Capital)
            && country.Capital.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(country.Region)
            && country.Region.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static int Rank(FlagdexCountry country, string query)
    {
        if (string.Equals(country.Name, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (country.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (country.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static bool TryExactIsoMatch(
        IReadOnlyList<FlagdexCountry> countries,
        string query,
        out FlagdexCountry match)
    {
        match = null!;
        var iso = query.Trim().ToUpperInvariant();
        if (!(iso.Length is 2 or 3) || !iso.All(char.IsLetter))
        {
            return false;
        }

        var found = countries.FirstOrDefault(country =>
            string.Equals(country.Iso2, iso, StringComparison.OrdinalIgnoreCase)
            || string.Equals(country.Iso3, iso, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return false;
        }

        match = found;
        return true;
    }

    private static bool TryMatchIso(string? value, out string iso)
    {
        iso = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (iso.Length == 2 && iso.All(char.IsLetter))
        {
            return true;
        }

        iso = string.Empty;
        return false;
    }

    private sealed class FlagdexCountry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("iso2")]
        public string Iso2 { get; set; } = string.Empty;

        [JsonPropertyName("iso3")]
        public string? Iso3 { get; set; }

        [JsonPropertyName("capital")]
        public string? Capital { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
    }
}
