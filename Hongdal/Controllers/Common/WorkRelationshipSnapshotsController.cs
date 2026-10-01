using Hongdal.Contracts.Common.Hr;
using Hongdal.Application.HumanResources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/work-relationship-snapshots")]
public sealed class WorkRelationshipSnapshotsController : ControllerBase
{
    private readonly I인연스냅샷조회UseCase _useCase;

    public WorkRelationshipSnapshotsController(I인연스냅샷조회UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet("me")]
    public async Task<ActionResult<WorkRelationshipSnapshotListResponse>> GetMine(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _useCase.내목록Async(take, cancellationToken));
    }
}
