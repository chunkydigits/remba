namespace Remba.Service.Entities;

/// <summary>
/// Open Library exposes free-text subjects (themes + genres mixed). This splits
/// them into coarse literary genres and remaining theme tags.
/// </summary>
public static class BookSubjectClassifier
{
    private static readonly (string Label, string[] Tokens)[] GenreRules =
    [
        ("Science Fiction", ["science fiction", "sci-fi", "sci fi", "sf fiction"]),
        ("Fantasy", ["fantasy"]),
        ("Horror", ["horror"]),
        ("Mystery", ["mystery", "detective"]),
        ("Thriller", ["thriller", "suspense"]),
        ("Romance", ["romance"]),
        ("Historical Fiction", ["historical fiction"]),
        ("Crime", ["crime fiction", "crime"]),
        ("Adventure", ["adventure"]),
        ("Graphic Novel", ["graphic novel", "comics", "comic books"]),
        ("Young Adult", ["young adult", "ya fiction"]),
        ("Children's", ["children's", "childrens", "juvenile fiction", "juvenile literature"]),
        ("Biography", ["biography", "autobiography", "memoir"]),
        ("History", ["history"]),
        ("Poetry", ["poetry", "poems"]),
        ("Drama", ["drama", "plays"]),
        ("Nonfiction", ["nonfiction", "non-fiction", "non fiction"]),
        ("Fiction", ["fiction"]),
    ];

    private static readonly string[] ThemeNoise =
    [
        "translation",
        "language materials",
        "reading level",
        "pictorial works",
        "specimens",
        "untranslated",
        "open library staff",
        "motion picture",
        "media tie-in",
        "general",
        "english literature",
        "english fiction",
        "british and irish",
        "characters and characteristics",
        "fictitious character",
        "personnages",
        "novela",
        "romans",
        "ficción",
        "literatura",
    ];

    public static (string? Genre, string? Themes) Classify(IEnumerable<string?>? subjects)
    {
        var cleaned = subjects?
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Select(subject => subject!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (cleaned.Count == 0)
        {
            return (null, null);
        }

        var genres = new List<string>();
        var usedForGenre = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, tokens) in GenreRules)
        {
            var match = cleaned.FirstOrDefault(subject =>
                tokens.Any(token => subject.Contains(token, StringComparison.OrdinalIgnoreCase)));
            if (match is null)
            {
                continue;
            }

            genres.Add(label);
            usedForGenre.Add(match);

            // Fiction is an umbrella — keep collecting more specific genres too.
            if (label is not "Fiction" and not "Nonfiction" && genres.Count >= 4)
            {
                break;
            }
        }

        // Prefer specific genres over the bare Fiction umbrella when both match.
        if (genres.Count > 1 && genres.Contains("Fiction", StringComparer.OrdinalIgnoreCase))
        {
            var specificFiction = genres.Any(genre =>
                genre is "Science Fiction" or "Fantasy" or "Horror" or "Mystery"
                    or "Thriller" or "Romance" or "Historical Fiction" or "Crime"
                    or "Adventure" or "Young Adult" or "Children's" or "Graphic Novel");
            if (specificFiction)
            {
                genres.RemoveAll(genre => string.Equals(genre, "Fiction", StringComparison.OrdinalIgnoreCase));
            }
        }

        var themes = cleaned
            .Where(subject => !usedForGenre.Contains(subject))
            .Where(subject => !IsThemeNoise(subject))
            .Where(subject => subject.Length is >= 3 and <= 48)
            .Where(subject => !genres.Any(genre =>
                subject.Contains(genre, StringComparison.OrdinalIgnoreCase)
                || genre.Contains(subject, StringComparison.OrdinalIgnoreCase)))
            .Take(6)
            .ToList();

        return (
            genres.Count > 0 ? string.Join(", ", genres) : null,
            themes.Count > 0 ? string.Join(", ", themes) : null);
    }

    private static bool IsThemeNoise(string subject)
    {
        var lower = subject.ToLowerInvariant();
        return ThemeNoise.Any(noise => lower.Contains(noise, StringComparison.Ordinal));
    }
}
