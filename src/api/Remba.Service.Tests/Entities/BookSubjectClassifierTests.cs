using Remba.Service.Entities;

namespace Remba.Service.Tests.Entities;

public class BookSubjectClassifierTests
{
    [Fact]
    public void Classify_WhenFantasySubjects_ThenShouldReturnFantasyGenreAndThemes()
    {
        var (genre, themes) = BookSubjectClassifier.Classify(
        [
            "Fantasy",
            "Fantasy fiction",
            "Fiction",
            "dragons",
            "hobbits",
            "quests",
            "Reading Level-Grade 11",
            "Translations into Russian"
        ]);

        Assert.Equal("Fantasy", genre);
        Assert.Contains("dragons", themes);
        Assert.Contains("hobbits", themes);
        Assert.DoesNotContain("Reading Level", themes ?? string.Empty);
    }

    [Fact]
    public void Classify_WhenScienceFiction_ThenShouldPreferScienceFictionOverFiction()
    {
        var (genre, _) = BookSubjectClassifier.Classify(
        [
            "Science fiction",
            "Fiction",
            "space travel"
        ]);

        Assert.Equal("Science Fiction", genre);
    }

    [Fact]
    public void Classify_WhenNonfiction_ThenShouldReturnNonfiction()
    {
        var (genre, _) = BookSubjectClassifier.Classify(["Non-fiction", "History"]);

        Assert.Contains("Nonfiction", genre);
        Assert.Contains("History", genre);
    }
}
