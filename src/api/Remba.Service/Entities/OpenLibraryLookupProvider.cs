using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Service.Entities;

public partial class OpenLibraryLookupProvider : IEntityLookupProvider
{
    private readonly HttpClient _httpClient;

    public OpenLibraryLookupProvider(
        HttpClient httpClient,
        ILogger<OpenLibraryLookupProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderKey => EntityLookupProviders.OpenLibrary;

    public bool SupportsEntityType(string entityType) =>
        string.Equals(entityType.Trim(), EntityTypes.Book, StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<EntitySearchResultDto>> SearchAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedQuery))
        {
            return [];
        }

        var digitsOnly = IsbnDigits().Replace(normalisedQuery, string.Empty);
        if (IsIsbn(digitsOnly))
        {
            var byIsbn = await GetByIsbnAsync(digitsOnly, cancellationToken).ConfigureAwait(false);
            return byIsbn is null ? [] : [byIsbn];
        }

        return await SearchByQueryAsync(normalisedQuery, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EntitySearchResultDto?> GetByIdentifierAsync(
        string entityType,
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsEntityType(entityType))
        {
            return null;
        }

        if (string.Equals(identifierType, EntityIdentifierTypes.Isbn, StringComparison.OrdinalIgnoreCase))
        {
            var digits = IsbnDigits().Replace(identifierValue, string.Empty);
            return IsIsbn(digits)
                ? await GetByIsbnAsync(digits, cancellationToken).ConfigureAwait(false)
                : null;
        }

        return null;
    }

    private async Task<EntitySearchResultDto?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        var url = $"api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn)}&format=json&jscmd=data";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<Dictionary<string, OpenLibraryBookData>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        var book = payload.Values.First();
        return MapBook(isbn, book);
    }

    private async Task<IReadOnlyList<EntitySearchResultDto>> SearchByQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        // Request isbn/key explicitly — default payloads often omit isbn on work hits.
        var url =
            $"search.json?q={Uri.EscapeDataString(query)}&limit=10" +
            "&fields=key,title,author_name,first_publish_year,isbn,cover_i,cover_edition_key";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<OpenLibrarySearchResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Docs is null || payload.Docs.Count == 0)
        {
            return [];
        }

        var results = new List<EntitySearchResultDto>();
        foreach (var doc in payload.Docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Title))
            {
                continue;
            }

            var mapped = MapSearchDoc(doc);
            if (mapped is not null)
            {
                results.Add(mapped);
            }
        }

        return results;
    }

    private EntitySearchResultDto? MapSearchDoc(OpenLibrarySearchDoc doc)
    {
        var isbn = doc.Isbn?
            .Select(value => IsbnDigits().Replace(value, string.Empty))
            .FirstOrDefault(IsIsbn);

        var authors = FormatAuthors(doc.AuthorName);

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            return new EntitySearchResultDto
            {
                Provider = ProviderKey,
                IdentifierType = EntityIdentifierTypes.Isbn,
                IdentifierValue = isbn,
                Name = doc.Title!,
                Year = doc.FirstPublishYear?.ToString(),
                Subtitle = authors,
                Author = authors,
                ImageUrl = CoverUrl(doc),
                Link = $"https://openlibrary.org/isbn/{isbn}"
            };
        }

        var workId = ExtractWorkId(doc.Key);
        if (string.IsNullOrWhiteSpace(workId))
        {
            return null;
        }

        return new EntitySearchResultDto
        {
            Provider = ProviderKey,
            IdentifierType = EntityIdentifierTypes.OpenLibraryWork,
            IdentifierValue = workId,
            Name = doc.Title!,
            Year = doc.FirstPublishYear?.ToString(),
            Subtitle = authors,
            Author = authors,
            ImageUrl = CoverUrl(doc),
            Link = $"https://openlibrary.org/works/{workId}"
        };
    }

    private static EntitySearchResultDto MapBook(string isbn, OpenLibraryBookData book)
    {
        var authors = FormatAuthors(book.Authors?.Select(author => author.Name));
        return new EntitySearchResultDto
        {
            Provider = EntityLookupProviders.OpenLibrary,
            IdentifierType = EntityIdentifierTypes.Isbn,
            IdentifierValue = isbn,
            Name = string.IsNullOrWhiteSpace(book.Title) ? $"ISBN {isbn}" : book.Title!,
            Year = book.PublishDate,
            Subtitle = authors,
            Author = authors,
            ImageUrl = book.Cover?.Medium ?? book.Cover?.Small,
            Link = $"https://openlibrary.org/isbn/{isbn}"
        };
    }

    private static string? FormatAuthors(IEnumerable<string?>? authors)
    {
        var names = authors?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(3)
            .ToList();

        return names is { Count: > 0 } ? string.Join(", ", names!) : null;
    }

    private static string? CoverUrl(OpenLibrarySearchDoc doc)
    {
        if (doc.CoverI is not null)
        {
            return $"https://covers.openlibrary.org/b/id/{doc.CoverI}-M.jpg";
        }

        if (!string.IsNullOrWhiteSpace(doc.CoverEditionKey))
        {
            return $"https://covers.openlibrary.org/b/olid/{doc.CoverEditionKey}-M.jpg";
        }

        return null;
    }

    private static string? ExtractWorkId(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        const string prefix = "/works/";
        var trimmed = key.Trim();
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..];
        }

        return trimmed.StartsWith("OL", StringComparison.OrdinalIgnoreCase) ? trimmed : null;
    }

    private static bool IsIsbn(string digits) => digits.Length is 10 or 13;

    [GeneratedRegex(@"[^\dXx]", RegexOptions.CultureInvariant)]
    private static partial Regex IsbnDigits();

    private sealed class OpenLibrarySearchResponse
    {
        [JsonPropertyName("docs")]
        public List<OpenLibrarySearchDoc>? Docs { get; set; }
    }

    private sealed class OpenLibrarySearchDoc
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("author_name")]
        public List<string>? AuthorName { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("isbn")]
        public List<string>? Isbn { get; set; }

        [JsonPropertyName("cover_i")]
        public long? CoverI { get; set; }

        [JsonPropertyName("cover_edition_key")]
        public string? CoverEditionKey { get; set; }
    }

    private sealed class OpenLibraryBookData
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("publish_date")]
        public string? PublishDate { get; set; }

        [JsonPropertyName("authors")]
        public List<OpenLibraryAuthor>? Authors { get; set; }

        [JsonPropertyName("cover")]
        public OpenLibraryCover? Cover { get; set; }
    }

    private sealed class OpenLibraryAuthor
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class OpenLibraryCover
    {
        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }
    }
}
