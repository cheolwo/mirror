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
    FeatureKey = VersionFeatureFlagKeys.RegionExperiencePackages,
    WorkflowKey = VersionFeatureFlagKeys.RegionExperiencePackages)]
[RequireVersionFeature(VersionFeatureFlagKeys.RegionExperiencePackages)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[Route(StationDioramaRoutes.Catalog)]
public sealed class 역세권디오라마Controller(I역세권디오라마조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Catalog(CancellationToken cancellationToken)
    {
        var value = await useCase.CatalogAsync(cancellationToken);
        return WithEtag(value, value.CatalogRevision, 300);
    }

    [HttpGet("{transitStationStableId}/diorama-manifest")]
    public async Task<IActionResult> Manifest(
        string transitStationStableId,
        CancellationToken cancellationToken)
    {
        if (!StationDioramaPolicy.IsTransitStationStableId(transitStationStableId))
            return BadRequest();
        var value = await useCase.ManifestAsync(transitStationStableId, cancellationToken);
        return value is null ? NotFound() : WithEtag(value, value.ManifestHashSha256, 60);
    }

    private IActionResult WithEtag(object value, string revision, int maxAgeSeconds)
    {
        var etag = "\"" + revision + "\"";
        if (Request.Headers.IfNoneMatch.Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = $"private, max-age={maxAgeSeconds}";
        return Ok(value);
    }
}
