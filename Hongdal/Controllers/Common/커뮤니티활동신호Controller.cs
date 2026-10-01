using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/activity-signals")]
public sealed class 커뮤니티활동신호Controller : ControllerBase
{
    private readonly I커뮤니티활동신호UseCase _useCase;

    public 커뮤니티활동신호Controller(I커뮤니티활동신호UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        [FromQuery] CommunityActivitySignalQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.조회Async(query, cancellationToken);
        return this.ToActionResult(result);
    }
}
