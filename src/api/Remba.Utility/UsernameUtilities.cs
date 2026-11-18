using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Remba.Utility;

/// <summary>
/// Helpers for provisional username generation until rename/uniqueness rules are finalised.
/// </summary>
public static partial class UsernameUtilities
{
    private static readonly Regex InvalidCharacters = InvalidUsernameCharacters();

    public static string CreateProvisionalUsername(string? preferred, string auth0Subject)
    {
        var seed = string.IsNullOrWhiteSpace(preferred) ? auth0Subject : preferred;
        var normalised = InvalidCharacters.Replace(seed.Trim().ToLowerInvariant(), string.Empty);

        if (normalised.Length > 24)
        {
            normalised = normalised[..24];
        }

        if (string.IsNullOrWhiteSpace(normalised))
        {
            normalised = "user";
        }

        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auth0Subject)))[..6]
            .ToLowerInvariant();

        return $"{normalised}-{suffix}";
    }

    [GeneratedRegex(@"[^a-z0-9._]", RegexOptions.Compiled)]
    private static partial Regex InvalidUsernameCharacters();
}
