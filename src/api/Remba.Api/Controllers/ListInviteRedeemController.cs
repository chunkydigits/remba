using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/list-invites")]
public class ListInviteRedeemController : ControllerBase
{
    private readonly IListInviteService _listInviteService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<ListInviteRedeemController> _logger;

    public ListInviteRedeemController(
        IListInviteService listInviteService,
        CurrentUserResolver currentUserResolver,
        ILogger<ListInviteRedeemController> logger)
    {
        _listInviteService = listInviteService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpPost("redeem")]
    [ProducesResponseType(typeof(RedeemListInviteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemListInviteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var result = await _listInviteService.RedeemAsync(user.Id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Invite code was not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to redeem invite code");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to redeem invite." });
        }
    }
}
