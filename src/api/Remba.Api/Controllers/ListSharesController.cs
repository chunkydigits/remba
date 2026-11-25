using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists/{listId}/shares")]
public class ListSharesController : ControllerBase
{
    private readonly IListShareService _listShareService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<ListSharesController> _logger;

    public ListSharesController(
        IListShareService listShareService,
        CurrentUserResolver currentUserResolver,
        ILogger<ListSharesController> logger)
    {
        _listShareService = listShareService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListShareMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(string listId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var members = await _listShareService.GetMembersAsync(user.Id, listId, cancellationToken);
            return Ok(members);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load shares for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load shares." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyList<ListShareMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Share(
        string listId,
        [FromBody] ShareListRequest request,
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

            var members = await _listShareService.ShareAsync(user.Id, listId, request, cancellationToken);
            return Ok(members);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List or share group was not found." });
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
            _logger.LogError(ex, "Failed to share list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to share list." });
        }
    }

    [HttpPut("{memberUserId}")]
    [ProducesResponseType(typeof(IReadOnlyList<ListShareMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole(
        string listId,
        string memberUserId,
        [FromBody] UpdateListShareRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var members = await _listShareService.UpdateMemberRoleAsync(
                user.Id,
                listId,
                memberUserId,
                request.Role,
                cancellationToken);
            return Ok(members);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Share was not found." });
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
            _logger.LogError(ex, "Failed to update share role for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update access level." });
        }
    }

    [HttpDelete("{memberUserId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unshare(
        string listId,
        string memberUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _listShareService.UnshareAsync(user.Id, listId, memberUserId, cancellationToken);
            return Ok(new { message = "Share removed." });
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
            _logger.LogError(ex, "Failed to unshare list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to remove share." });
        }
    }

    [HttpPost("leave")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leave(string listId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _listShareService.LeaveAsync(user.Id, listId, cancellationToken);
            return Ok(new { message = "Left shared list." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to leave list." });
        }
    }
}
