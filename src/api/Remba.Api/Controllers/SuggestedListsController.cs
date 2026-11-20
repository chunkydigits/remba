using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Model.System;
using Remba.Service.System;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suggested-lists")]
public class SuggestedListsController : ControllerBase
{
    private readonly ISuggestedListService _suggestedListService;
    private readonly ILogger<SuggestedListsController> _logger;

    public SuggestedListsController(
        ISuggestedListService suggestedListService,
        ILogger<SuggestedListsController> logger)
    {
        _suggestedListService = suggestedListService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SuggestedListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _suggestedListService.GetAllAsync(includeDisabled, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load suggested lists");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to load suggested lists." });
        }
    }

    [HttpGet("{seedKey}")]
    [ProducesResponseType(typeof(SuggestedListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBySeedKey(string seedKey, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _suggestedListService.GetBySeedKeyAsync(seedKey, cancellationToken);
            if (item is null)
            {
                return NotFound(new { message = $"Suggested list '{seedKey}' was not found." });
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load suggested list {SeedKey}", seedKey);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to load suggested list." });
        }
    }

    [HttpPut("{seedKey}")]
    [ProducesResponseType(typeof(SuggestedListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        string seedKey,
        [FromBody] UpdateSuggestedListRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _suggestedListService.UpdateAsync(seedKey, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update suggested list {SeedKey}", seedKey);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to update suggested list." });
        }
    }
}
