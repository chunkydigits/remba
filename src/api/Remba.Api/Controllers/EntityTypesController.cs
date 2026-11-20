using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remba.Api.Auth;
using Remba.Model.System;
using Remba.Service.System;

namespace Remba.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/entity-types")]
public class EntityTypesController : ControllerBase
{
    private readonly IEntityTypeService _entityTypeService;
    private readonly CurrentUserResolver _currentUserResolver;
    private readonly ILogger<EntityTypesController> _logger;

    public EntityTypesController(
        IEntityTypeService entityTypeService,
        CurrentUserResolver currentUserResolver,
        ILogger<EntityTypesController> logger)
    {
        _entityTypeService = entityTypeService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EntityTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAvailable(CancellationToken cancellationToken)
    {
        try
        {
            var user = await _currentUserResolver.ResolveAsync(User, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Authenticated subject was not found on the token." });
            }

            var items = await _entityTypeService.GetAvailableAsync(user.Id, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entity types");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to load entity types." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEntityTypeRequest request,
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

            var created = await _entityTypeService.CreatePrivateAsync(user.Id, request, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create entity type");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to create entity type." });
        }
    }
}
