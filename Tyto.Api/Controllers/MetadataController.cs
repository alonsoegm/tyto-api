using Microsoft.AspNetCore.Mvc;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.Metadata;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Controllers;

/// <summary>
/// Exposes provider-agnostic metadata (entities and fields) for a saved database connection,
/// so the UI can dynamically populate target tables/columns and validate mappings.
/// </summary>
[ApiController]
[Route("api/metadata")]
public class MetadataController : ControllerBase
{
    private readonly IMetadataService _service;

    public MetadataController(IMetadataService service) => _service = service;

    /// <summary>Returns the entities (tables) exposed by the given database connection.</summary>
    [HttpGet("entities")]
    public async Task<ActionResult<ApiResponse<List<EntityDto>>>> GetEntities(
        [FromQuery] Guid databaseConnectionId, CancellationToken cancellationToken)
    {
        var result = await _service.GetEntitiesAsync(databaseConnectionId, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns the fields (columns) for the given entity within the database connection.</summary>
    [HttpGet("entities/{entityId}/fields")]
    public async Task<ActionResult<ApiResponse<List<FieldDto>>>> GetFields(
        string entityId, [FromQuery] Guid databaseConnectionId, CancellationToken cancellationToken)
    {
        var result = await _service.GetFieldsAsync(databaseConnectionId, entityId, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }
}
