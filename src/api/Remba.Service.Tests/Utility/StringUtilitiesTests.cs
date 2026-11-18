using Remba.Utility;

namespace Remba.Service.Tests.Utility;

public class StringUtilitiesTests
{
    [Fact]
    public void NormalizeWhitespace_WhenNull_ThenReturnsEmptyString()
    {
        var result = StringUtilities.NormalizeWhitespace(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizeWhitespace_WhenExtraSpaces_ThenCollapsesWhitespace()
    {
        var result = StringUtilities.NormalizeWhitespace("  Films   to  Watch  ");

        Assert.Equal("Films to Watch", result);
    }
}
