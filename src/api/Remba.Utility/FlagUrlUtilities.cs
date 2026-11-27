namespace Remba.Utility;

/// <summary>
/// Builds public Flagdex image URLs.
/// On Linux, <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> treats a rooted path such as
/// "/flags/nz.svg" as an absolute file URI. Callers must not pass that result through as the image URL.
/// </summary>
public static class FlagUrlUtilities
{
    public const string DefaultBaseUrl = "https://flagdex.net";

    public static string Build(string iso2, string? flagPath, string? flagBaseUrl = null)
    {
        var flagBase = string.IsNullOrWhiteSpace(flagBaseUrl)
            ? DefaultBaseUrl
            : flagBaseUrl.Trim().TrimEnd('/');

        if (IsHttpUrl(flagPath))
        {
            return flagPath!.Trim();
        }

        if (TryGetFlagPath(flagPath, out var path))
        {
            return flagBase + path;
        }

        return $"{flagBase}/flags/{iso2.Trim().ToLowerInvariant()}.svg";
    }

    /// <summary>
    /// Rewrites a stored flag path or file URI to the public Flagdex URL.
    /// Other image URLs are returned unchanged.
    /// </summary>
    public static string? Normalise(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (TryGetFlagPath(url, out var path))
        {
            return DefaultBaseUrl + path;
        }

        return url.Trim();
    }

    private static bool TryGetFlagPath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (IsRootedFlagPath(trimmed))
        {
            path = trimmed;
            return true;
        }

        if (!trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var flagsIndex = trimmed.IndexOf("/flags/", StringComparison.OrdinalIgnoreCase);
        if (flagsIndex < 0)
        {
            return false;
        }

        var candidate = trimmed[flagsIndex..];
        if (!IsRootedFlagPath(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static bool IsRootedFlagPath(string value) =>
        value.StartsWith("/flags/", StringComparison.OrdinalIgnoreCase)
        && value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains("://", StringComparison.Ordinal);

    private static bool IsHttpUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
