using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(
    SsalddelProductVersion.V0_0,
    FeatureKey = VersionFeatureFlagKeys.AdministrativeDongDioramaObservation,
    WorkflowKey = VersionFeatureFlagKeys.AdministrativeDongDioramaObservation)]
[RequireVersionFeature(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[Route("api/v1/world/administrative-areas/{administrativeAreaStableId}")]
public sealed class 행정동디오라마Controller(I행정동디오라마조회UseCase useCase) : ControllerBase
{
    [HttpGet("diorama-manifest")]
    public async Task<IActionResult> Manifest(string administrativeAreaStableId, CancellationToken cancellationToken)
    {
        var value = await useCase.ManifestAsync(administrativeAreaStableId, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.ProjectionHashSha256);
    }

    [HttpGet("diorama-tiles/{tileStableId}")]
    public async Task<IActionResult> Tile(
        string administrativeAreaStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        var value = await useCase.TileAsync(administrativeAreaStableId, tileStableId, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.TileHashSha256);
    }

    [HttpGet("display-overlays")]
    public async Task<IActionResult> DisplayOverlays(string administrativeAreaStableId, CancellationToken cancellationToken)
    {
        var value = await useCase.DisplayOverlaysAsync(administrativeAreaStableId, DateTime.UtcNow, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.OverlayRevision);
    }

    private IActionResult WithEtag(object value, string revision)
    {
        var etag = "\"" + revision + "\"";
        if (Request.Headers.IfNoneMatch.Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=30";
        return Ok(value);
    }
}
