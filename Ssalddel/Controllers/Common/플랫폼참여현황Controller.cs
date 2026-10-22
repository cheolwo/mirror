using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Home;
using Ssalddel.Contracts.Common.Home;

namespace Ssalddel.Controllers.Common;

[ApiController]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiAudience(SsalddelActor.CommunityMember)]
[SsalddelApiContractName("HomeParticipationSummaryController")]
[Route("api/v1/common/home/participation-summary")]
public sealed class 플랫폼참여현황Controller(I플랫폼참여현황조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HomeParticipationSummaryResponse>> 조회(CancellationToken cancellationToken)
        => Ok(await useCase.조회Async(cancellationToken));

    [HttpGet("~/api/v1/common/home/participation-details")]
    public async Task<ActionResult<HomeParticipationDetailsResponse>> 상세조회(
        [FromServices] I플랫폼참여상세조회UseCase details,
        [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 10000)
            return BadRequest(new ProblemDetails { Status = 400, Title = "조회할 페이지를 확인해 주세요." });
        return Ok(await details.상세조회Async(page, cancellationToken));
    }
}
