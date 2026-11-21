using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.Lists;
using Remba.Service.Lists;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists/{listId}/entries")]
public class EntriesController : ControllerBase
{
    private readonly IEntryService _entryService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<EntriesController> _logger;

    public EntriesController(
        IEntryService entryService,
        CurrentUserResolver currentUserResolver,
        ILogger<EntriesController> logger)
    {
        _entryService = entryService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByList(
        string listId,
        [FromQuery] string? q,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? author,
        [FromQuery] string? director,
        [FromQuery] string? actors,
        [FromQuery] string? genre,
        [FromQuery] string? themes,
        [FromQuery] bool? read,
        [FromQuery] bool? visited,
        [FromQuery] bool? seen,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var entries = await _entryService.GetByListAsync(
                user.Id,
                listId,
                new EntryQuery
                {
                    Q = q,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    Author = author,
                    Director = director,
                    Actors = actors,
                    Genre = genre,
                    Themes = themes,
                    Read = read,
                    Visited = visited,
                    Seen = seen
                },
                cancellationToken);
            return Ok(entries);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entries for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load entries." });
        }
    }

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        string listId,
        [FromBody] ReorderEntriesRequest request,
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

            await _entryService.ReorderAsync(user.Id, listId, request, cancellationToken);
            return Ok(new { message = "Entries reordered." });
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
            _logger.LogError(ex, "Failed to reorder entries for list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to reorder entries." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        string listId,
        [FromBody] CreateEntryRequest request,
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

            var created = await _entryService.CreateAsync(user.Id, listId, request, cancellationToken);
            return Ok(created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
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
            _logger.LogError(ex, "Failed to create entry on list {ListId}", listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create entry." });
        }
    }

    [HttpGet("{entryId}")]
    [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwn(string listId, string entryId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var entry = await _entryService.GetOwnAsync(user.Id, listId, entryId, cancellationToken);
            return entry is null ? NotFound(new { message = "Entry was not found." }) : Ok(entry);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "List was not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entry {EntryId} on list {ListId}", entryId, listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to load entry." });
        }
    }

    [HttpPut("{entryId}")]
    [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string listId,
        string entryId,
        [FromBody] UpdateEntryRequest request,
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

            var updated = await _entryService.UpdateAsync(user.Id, listId, entryId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update entry {EntryId} on list {ListId}", entryId, listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update entry." });
        }
    }

    [HttpDelete("{entryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(string listId, string entryId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            await _entryService.SoftDeleteAsync(user.Id, listId, entryId, cancellationToken);
            return Ok(new { message = "Entry deleted." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete entry {EntryId} on list {ListId}", entryId, listId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete entry." });
        }
    }
}
