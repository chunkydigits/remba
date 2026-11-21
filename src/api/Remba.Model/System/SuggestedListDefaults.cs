using Remba.Model.Entities;
using Remba.Model.Lists;

namespace Remba.Model.System;

/// <summary>
/// Built-in suggested list templates. Seeded only when a seedKey is missing —
/// existing documents are never overwritten.
/// </summary>
public static class SuggestedListDefaults
{
    public static IReadOnlyList<SuggestedListDbo> CreateAll(DateTime utcNow) =>
    [
        CreateFilms(utcNow),
        CreateBooks(utcNow),
        CreateRecipes(utcNow),
        CreateTv(utcNow),
        CreateCountries(utcNow)
    ];

    public static SuggestedListDbo CreateFilms(DateTime utcNow) =>
        Create(
            utcNow,
            SuggestedListSeedKeys.Films,
            name: "Films",
            description: "Films to watch, watched, or remember.",
            sortOrder: 1,
            entityType: "film",
            identifierType: SuggestedListIdentifierTypes.ImdbId,
            identifierLabel: "IMDB ID",
            urlTemplate: "https://www.imdb.com/title/{value}/",
            provider: "imdb",
            availableAttributes:
            [
                Attr("title", "Title"),
                Attr("year", "Year", "number"),
                Attr("director", "Director"),
                Attr("genre", "Genre"),
                Attr("rating", "Rating", "number")
            ],
            defaultSelectedKeys: ["title", "year", "director", "genre"]);

    public static SuggestedListDbo CreateBooks(DateTime utcNow) =>
        Create(
            utcNow,
            SuggestedListSeedKeys.Books,
            name: "Books",
            description: "Books to read, reading, or remember.",
            sortOrder: 2,
            entityType: "book",
            identifierType: SuggestedListIdentifierTypes.Isbn,
            identifierLabel: "ISBN",
            urlTemplate: "https://openlibrary.org/isbn/{value}",
            provider: "openlibrary",
            availableAttributes:
            [
                Attr("title", "Title"),
                Attr("authors", "Authors"),
                Attr("genre", "Genre"),
                Attr("publishYear", "Publish year", "number"),
                Attr("coverUrl", "Cover URL")
            ],
            defaultSelectedKeys: ["title", "authors", "genre"]);

    public static SuggestedListDbo CreateRecipes(DateTime utcNow) =>
        Create(
            utcNow,
            SuggestedListSeedKeys.Recipes,
            name: "Recipes",
            description: "Recipes to try or keep for reference.",
            sortOrder: 3,
            entityType: "recipe",
            identifierType: SuggestedListIdentifierTypes.Url,
            identifierLabel: "URL",
            urlTemplate: "{value}",
            provider: "url",
            availableAttributes:
            [
                Attr("title", "Title"),
                Attr("siteName", "Site"),
                Attr("imageUrl", "Image URL")
            ],
            defaultSelectedKeys: ["title", "siteName"]);

    public static SuggestedListDbo CreateTv(DateTime utcNow) =>
        Create(
            utcNow,
            SuggestedListSeedKeys.Tv,
            name: "TV",
            description: "TV shows to watch, watching, or remember.",
            sortOrder: 4,
            entityType: "tv",
            identifierType: SuggestedListIdentifierTypes.ImdbId,
            identifierLabel: "IMDB ID",
            urlTemplate: "https://www.imdb.com/title/{value}/",
            provider: "imdb",
            availableAttributes:
            [
                Attr("title", "Title"),
                Attr("year", "Year", "number"),
                Attr("creators", "Creators"),
                Attr("genre", "Genre"),
                Attr("rating", "Rating", "number")
            ],
            defaultSelectedKeys: ["title", "year", "genre"]);

    public static SuggestedListDbo CreateCountries(DateTime utcNow) =>
        Create(
            utcNow,
            SuggestedListSeedKeys.Countries,
            name: "Countries",
            description: "Countries you've been to, or want to visit.",
            sortOrder: 5,
            entityType: "country",
            identifierType: SuggestedListIdentifierTypes.Iso2,
            identifierLabel: "ISO code",
            urlTemplate: "https://flagdex.net/flags/{value}.svg",
            provider: EntityLookupProviders.Flagdex,
            availableAttributes:
            [
                Attr("title", "Name"),
                Attr("iso2", "ISO code"),
                Attr("region", "Region"),
                Attr("capital", "Capital"),
                Attr("flagUrl", "Flag URL")
            ],
            defaultSelectedKeys: ["title", "region", "flagUrl"]);

    private static SuggestedListDbo Create(
        DateTime utcNow,
        string seedKey,
        string name,
        string description,
        int sortOrder,
        string entityType,
        string identifierType,
        string identifierLabel,
        string urlTemplate,
        string provider,
        IEnumerable<SuggestedListEnrichmentAttributeDbo> availableAttributes,
        IEnumerable<string> defaultSelectedKeys) =>
        new()
        {
            Id = seedKey,
            RecordType = SystemRecordTypes.SuggestedList,
            SeedKey = seedKey,
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            Enabled = true,
            EntityType = entityType,
            Identifier = new SuggestedListIdentifierDbo
            {
                Type = identifierType,
                Label = identifierLabel,
                UrlTemplate = urlTemplate
            },
            Enrichment = new SuggestedListEnrichmentDbo
            {
                Provider = provider,
                AvailableAttributes = availableAttributes.ToList(),
                DefaultSelectedAttributeKeys = defaultSelectedKeys.ToList()
            },
            DefaultEntryAttributes =
            [
                new ListAttributeDefinitionDbo
                {
                    Key = "recommendedBy",
                    Name = "Recommended by",
                    Type = "text",
                    Scope = "entry"
                },
                new ListAttributeDefinitionDbo
                {
                    Key = "recommendedOn",
                    Name = "Recommended on",
                    Type = "date",
                    Scope = "entry"
                }
            ],
            Added = utcNow,
            Edited = null
        };

    private static SuggestedListEnrichmentAttributeDbo Attr(string key, string name, string type = "text") =>
        new()
        {
            Key = key,
            Name = name,
            Type = type
        };
}
