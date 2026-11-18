using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Users;
using Remba.Service.Users;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<MeController> _logger;

    public MeController(IUserService userService, ILogger<MeController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current Remba user, creating the account on first successful sign-in.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        try
        {
            var auth0Subject = User.GetAuth0Subject();
            if (string.IsNullOrWhiteSpace(auth0Subject))
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var user = await _userService.EnsureCurrentUserAsync(
                auth0Subject,
                User.GetPreferredDisplayName(),
                User.GetUsernameSeed(),
                User.GetEmail(),
                cancellationToken);

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve current user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to resolve the current user." });
        }
    }

    /// <summary>
    /// Updates the signed-in user's profile fields.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateCurrentUser(
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var auth0Subject = User.GetAuth0Subject();
            if (string.IsNullOrWhiteSpace(auth0Subject))
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var current = await _userService.EnsureCurrentUserAsync(
                auth0Subject,
                User.GetPreferredDisplayName(),
                User.GetUsernameSeed(),
                User.GetEmail(),
                cancellationToken);

            var updated = await _userService.UpdateCurrentUserAsync(current.Id, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update current user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update the current user." });
        }
    }
}
