using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize(Roles = "서버관리자")]
[SsalddelApiVersion(
    SsalddelProductVersion.V0_0,
    FeatureKey = VersionFeatureFlagKeys.RegionMobilityObservation,
    WorkflowKey = VersionFeatureFlagKeys.RegionMobilityObservation)]
[RequireVersionFeature(VersionFeatureFlagKeys.RegionMobilityObservation)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[Route("api/v1/world/regions/{regionStableId}")]
public sealed class 지역이동망Controller(
    I지역이동망조회UseCase useCase,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet("mobility-graph-manifest")]
    public async Task<IActionResult> Manifest(
        string regionStableId,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var value = await useCase.ManifestAsync(regionStableId, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.ProjectionHashSha256);
    }

    [HttpGet("mobility-graph-tiles/{tileStableId}")]
    public async Task<IActionResult> Tile(
        string regionStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var value = await useCase.TileAsync(regionStableId, tileStableId, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.ContentHashSha256);
    }

    private IActionResult WithEtag(object value, string revision)
    {
        var etag = "\"" + revision + "\"";
        if (Request.Headers.IfNoneMatch.Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, no-cache";
        return Ok(value);
    }
}
