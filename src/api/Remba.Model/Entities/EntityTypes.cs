namespace Remba.Model.Entities;

public static class EntityTypes
{
    public const string Film = "film";

    public const string Tv = "tv";

    public const string Book = "book";

    public const string Recipe = "recipe";

    public const string Country = "country";
}

public static class EntityIdentifierTypes
{
    public const string ImdbId = "imdbId";

    public const string Isbn = "isbn";

    public const string OpenLibraryWork = "openLibraryWork";

    public const string Iso2 = "iso2";

    public const string Url = "url";
}

public static class EntityLookupProviders
{
    public const string Imdb = "imdb";

    public const string OpenLibrary = "openlibrary";

    public const string Flagdex = "flagdex";

    public const string Remba = "remba";
}
