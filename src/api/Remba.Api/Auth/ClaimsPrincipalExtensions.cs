using System.Security.Claims;
using Remba.Utility;

namespace Remba.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string? GetAuth0Subject(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue("sub");
    }

    public static string? GetPreferredDisplayName(this ClaimsPrincipal principal)
    {
        var namespacedName = principal.FindFirstValue(Auth0ClaimTypes.Name);
        if (!StringUtilities.IsNullOrWhiteSpace(namespacedName))
        {
            return namespacedName;
        }

        var givenName = principal.FindFirstValue(Auth0ClaimTypes.GivenName);
        var familyName = principal.FindFirstValue(Auth0ClaimTypes.FamilyName);
        var combined = StringUtilities.NormalizeWhitespace($"{givenName} {familyName}");
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        return principal.FindFirstValue("name")
               ?? principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.FindFirstValue("nickname");
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(Auth0ClaimTypes.Email)
               ?? principal.FindFirstValue(ClaimTypes.Email)
               ?? principal.FindFirstValue("email");
    }

    public static string? GetUsernameSeed(this ClaimsPrincipal principal)
    {
        var email = principal.GetEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var atIndex = email.IndexOf('@');
            return atIndex > 0 ? email[..atIndex] : email;
        }

        var givenName = principal.FindFirstValue(Auth0ClaimTypes.GivenName);
        if (!StringUtilities.IsNullOrWhiteSpace(givenName))
        {
            return givenName;
        }

        return principal.FindFirstValue("nickname");
    }
}
