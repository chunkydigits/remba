using System.Security.Claims;
using Remba.Model.Users;
using Remba.Service.Users;

namespace Remba.Api.Auth;

public class CurrentUserResolver(IUserService userService)
{
    public async Task<UserDto?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var auth0Subject = principal.GetAuth0Subject();
        if (string.IsNullOrWhiteSpace(auth0Subject))
        {
            return null;
        }

        return await userService.EnsureCurrentUserAsync(
            auth0Subject,
            principal.GetPreferredDisplayName(),
            principal.GetUsernameSeed(),
            principal.GetEmail(),
            cancellationToken);
    }
}
