using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/share-groups")]
public class ShareGroupsController : ControllerBase
{
    private readonly IShareGroupService _shareGroupService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<ShareGroupsController> _logger;

    public ShareGroupsController(
        IShareGroupService shareGroupService,
        CurrentUserResolver currentUserResolver,
        ILogger<ShareGroupsController> logger)
    {
        _shareGroupService = shareGroupService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ShareGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOwn(CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var groups = await _shareGroupService.GetOwnAsync(user.Id, cancellationToken);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load share groups");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load share groups." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ShareGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShareGroupRequest request,
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

            var created = await _shareGroupService.CreateAsync(user.Id, request, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create share group");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create share group." });
        }
    }

    [HttpPut("{groupId}")]
    [ProducesResponseType(typeof(ShareGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string groupId,
        [FromBody] UpdateShareGroupRequest request,
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

            var updated = await _shareGroupService.UpdateAsync(user.Id, groupId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Share group was not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update share group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update share group." });
        }
    }

    [HttpDelete("{groupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string groupId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _shareGroupService.DeleteAsync(user.Id, groupId, cancellationToken);
            return Ok(new { message = "Share group deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Share group was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete share group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete share group." });
        }
    }
}
