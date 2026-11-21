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
        // Prefer English editions: language:eng requires an English edition; lang=en
        // boosts the English edition into the editions payload (title/isbn).
        var englishQuery = $"{query} language:eng";
        var url =
            $"search.json?q={Uri.EscapeDataString(englishQuery)}" +
            "&lang=en&limit=10" +
            "&fields=key,title,author_name,first_publish_year,isbn,cover_i,cover_edition_key,language,subject,subject_key," +
            "editions,editions.key,editions.title,editions.isbn,editions.language";
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
            if (!HasEnglishLanguage(doc))
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
        var edition = PreferEnglishEdition(doc);
        var title = StringUtilities.NormalizeWhitespace(edition?.Title)
            ?? StringUtilities.NormalizeWhitespace(doc.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var isbn = FirstIsbn(edition?.Isbn) ?? FirstIsbn(doc.Isbn);
        var authors = FormatAuthors(doc.AuthorName);
        var (genre, themes) = BookSubjectClassifier.Classify(doc.Subject);

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            return new EntitySearchResultDto
            {
                Provider = ProviderKey,
                IdentifierType = EntityIdentifierTypes.Isbn,
                IdentifierValue = isbn,
                Name = title,
                Year = doc.FirstPublishYear?.ToString(),
                Subtitle = authors,
                Author = authors,
                Genre = genre,
                Themes = themes,
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
            Name = title,
            Year = doc.FirstPublishYear?.ToString(),
            Subtitle = authors,
            Author = authors,
            Genre = genre,
            Themes = themes,
            ImageUrl = CoverUrl(doc),
            Link = $"https://openlibrary.org/works/{workId}"
        };
    }

    private static bool HasEnglishLanguage(OpenLibrarySearchDoc doc)
    {
        if (doc.Language is { Count: > 0 })
        {
            return doc.Language.Any(IsEnglishLanguageCode);
        }

        var edition = PreferEnglishEdition(doc);
        return edition?.Language is null
            || edition.Language.Count == 0
            || edition.Language.Any(IsEnglishLanguageCode);
    }

    private static OpenLibraryEditionDoc? PreferEnglishEdition(OpenLibrarySearchDoc doc)
    {
        var editions = doc.Editions?.Docs;
        if (editions is null || editions.Count == 0)
        {
            return null;
        }

        return editions.FirstOrDefault(edition =>
                   edition.Language is { Count: > 0 } && edition.Language.Any(IsEnglishLanguageCode))
               ?? editions[0];
    }

    private static string? FirstIsbn(IEnumerable<string>? values) =>
        values?
            .Select(value => IsbnDigits().Replace(value, string.Empty))
            .FirstOrDefault(IsIsbn);

    private static bool IsEnglishLanguageCode(string? code) =>
        string.Equals(code?.Trim(), "eng", StringComparison.OrdinalIgnoreCase);

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

        [JsonPropertyName("subject")]
        public List<string>? Subject { get; set; }

        [JsonPropertyName("language")]
        public List<string>? Language { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("isbn")]
        public List<string>? Isbn { get; set; }

        [JsonPropertyName("cover_i")]
        public long? CoverI { get; set; }

        [JsonPropertyName("cover_edition_key")]
        public string? CoverEditionKey { get; set; }

        [JsonPropertyName("editions")]
        public OpenLibraryEditionsPayload? Editions { get; set; }
    }

    private sealed class OpenLibraryEditionsPayload
    {
        [JsonPropertyName("docs")]
        public List<OpenLibraryEditionDoc>? Docs { get; set; }
    }

    private sealed class OpenLibraryEditionDoc
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("isbn")]
        public List<string>? Isbn { get; set; }

        [JsonPropertyName("language")]
        public List<string>? Language { get; set; }
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
