namespace Remba.Api.Auth;

/// <summary>
/// Custom access-token claims added by the Auth0 post-login action.
/// </summary>
public static class Auth0ClaimTypes
{
    public const string Namespace = "https://remba.it/";

    public const string Name = Namespace + "name";

    public const string Email = Namespace + "email";

    public const string GivenName = Namespace + "given_name";

    public const string FamilyName = Namespace + "family_name";
}
