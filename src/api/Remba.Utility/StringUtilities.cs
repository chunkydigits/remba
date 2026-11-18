namespace Remba.Utility;

/// <summary>
/// Shared string helpers used across API layers.
/// </summary>
public static class StringUtilities
{
    /// <summary>
    /// Returns true when the value is null, empty, or whitespace.
    /// </summary>
    public static bool IsNullOrWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Trims and collapses internal whitespace; returns empty string for null input.
    /// </summary>
    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
