using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Model.Entities;
using Remba.Service.Entities;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/entities")]
public class EntitiesController : ControllerBase
{
    private readonly IEntityLookupService _entityLookupService;
    private readonly ILogger<EntitiesController> _logger;

    public EntitiesController(
        IEntityLookupService entityLookupService,
        ILogger<EntitiesController> logger)
    {
        _entityLookupService = entityLookupService;
        _logger = logger;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<EntitySearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search(
        [FromQuery] string entityType,
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _entityLookupService.SearchAsync(entityType, q, cancellationToken);
            return Ok(results);
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
            _logger.LogError(ex, "Failed to search entities for type {EntityType}", entityType);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to search entities." });
        }
    }
}
