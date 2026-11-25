using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists/{listId}/invites")]
public class ListInvitesController : ControllerBase
{
    private readonly IListInviteService _listInviteService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<ListInvitesController> _logger;

    public ListInvitesController(
        IListInviteService listInviteService,
        CurrentUserResolver currentUserResolver,
        ILogger<ListInvitesController> logger)
    {
        _listInviteService = listInviteService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByList(string listId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var invites = await _listInviteService.GetByListAsync(user.Id, listId, cancellationToken);
            return Ok(invites);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invites for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load invites." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ListInviteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        string listId,
        [FromBody] CreateListInviteRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var created = await _listInviteService.CreateAsync(
                user.Id,
                listId,
                request ?? new CreateListInviteRequest(),
                cancellationToken);
            return Ok(created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create invite for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create invite." });
        }
    }

    [HttpDelete("{inviteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string listId, string inviteId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _listInviteService.DeleteAsync(user.Id, listId, inviteId, cancellationToken);
            return Ok(new { message = "Invite deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Invite was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete invite {InviteId} for list {ListId}", inviteId, listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete invite." });
        }
    }
}
