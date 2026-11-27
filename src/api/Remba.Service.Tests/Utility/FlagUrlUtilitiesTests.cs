using Remba.Utility;

namespace Remba.Service.Tests.Utility;

public class FlagUrlUtilitiesTests
{
    [Theory]
    [InlineData("/flags/nz.svg")]
    [InlineData("file:///flags/nz.svg")]
    [InlineData("file://flags/nz.svg")]
    public void Build_WhenFlagdexReturnsRootedOrFilePath_ThenUsesHttpsFlagUrl(string flagPath)
    {
        var result = FlagUrlUtilities.Build("NZ", flagPath);

        Assert.Equal("https://flagdex.net/flags/nz.svg", result);
    }

    [Fact]
    public void Build_WhenFlagPathIsAlreadyHttps_ThenKeepsIt()
    {
        var result = FlagUrlUtilities.Build("NZ", "https://flagdex.net/flags/nz.svg");

        Assert.Equal("https://flagdex.net/flags/nz.svg", result);
    }

    [Fact]
    public void Normalise_WhenStoredFileUri_ThenRewritesToFlagdex()
    {
        var result = FlagUrlUtilities.Normalise("file:///flags/nz.svg");

        Assert.Equal("https://flagdex.net/flags/nz.svg", result);
    }

    [Fact]
    public void Normalise_WhenPosterUrl_ThenLeavesItUnchanged()
    {
        const string poster = "https://image.tmdb.org/t/p/w92/abc.jpg";

        var result = FlagUrlUtilities.Normalise(poster);

        Assert.Equal(poster, result);
    }
}
