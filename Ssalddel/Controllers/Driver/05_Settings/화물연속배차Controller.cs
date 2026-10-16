using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Settings05;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiCapability(SsalddelCapability.Dispatch)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/operational-dispatch/continuity")]
public sealed class 화물연속배차Controller : DriverControllerBase
{
    private readonly I화물연속배차UseCase _useCase;

    public 화물연속배차Controller(I화물연속배차UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    public async Task<ActionResult<화물연속배차상태Dto>> 조회(CancellationToken cancellationToken)
        => Ok(await _useCase.조회Async(현재기사Id(), cancellationToken));

    [HttpPut]
    public async Task<ActionResult<화물연속배차상태Dto>> 변경(
        [FromBody] 화물연속배차의사변경요청 request,
        CancellationToken cancellationToken)
        => Ok(await _useCase.의사변경Async(현재기사Id(), request, cancellationToken));
}
