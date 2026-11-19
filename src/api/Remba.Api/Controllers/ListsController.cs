using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists")]
public class ListsController : ControllerBase
{
    private readonly IListService _listService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<ListsController> _logger;

    public ListsController(
        IListService listService,
        CurrentUserResolver currentUserResolver,
        ILogger<ListsController> logger)
    {
        _listService = listService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOwnLists(CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var lists = await _listService.GetOwnListsAsync(user.Id, cancellationToken);
            return Ok(lists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load lists");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load lists." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateListRequest request, CancellationToken cancellationToken)
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

            var created = await _listService.CreateAsync(user.Id, request, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create list");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create list." });
        }
    }

    [HttpGet("{listId}")]
    [ProducesResponseType(typeof(ListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwn(string listId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var list = await _listService.GetOwnAsync(user.Id, listId, cancellationToken);
            return list is null ? NotFound(new { message = "List was not found." }) : Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load list." });
        }
    }

    [HttpPut("{listId}")]
    [ProducesResponseType(typeof(ListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string listId,
        [FromBody] UpdateListRequest request,
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

            var updated = await _listService.UpdateAsync(user.Id, listId, request, cancellationToken);
            return Ok(updated);
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
            _logger.LogError(ex, "Failed to update list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update list." });
        }
    }

    [HttpPut("{listId}/view-settings")]
    [ProducesResponseType(typeof(ListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateViewSettings(
        string listId,
        [FromBody] UpdateListViewSettingsRequest request,
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

            var updated = await _listService.UpdateViewSettingsAsync(user.Id, listId, request, cancellationToken);
            return Ok(updated);
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
            _logger.LogError(ex, "Failed to update view settings for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update view settings." });
        }
    }

    [HttpDelete("{listId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string listId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _listService.DeleteAsync(user.Id, listId, cancellationToken);
            return Ok(new { message = "List deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete list." });
        }
    }
}
