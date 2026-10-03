using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Settings05;

[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.OperationalDispatchCore)]
[SsalddelApiCapability(SsalddelCapability.Dispatch)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[SsalddelApiContractName("OperationalDispatchCoreController")]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/operational-dispatch")]
public sealed class 기사운영배차공통Controller : DriverControllerBase
{
    private readonly I운영배차공통UseCase _useCase;

    public 기사운영배차공통Controller(I운영배차공통UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet("availability")]
    public async Task<ActionResult<운영배차수신상태Dto>> 수신상태조회(CancellationToken cancellationToken)
        => Ok(await _useCase.수신상태조회Async(CurrentDriverId(), cancellationToken));

    [HttpPut("availability/intent")]
    public async Task<ActionResult<운영배차수신상태Dto>> 수신의사변경(
        [FromBody] 운영배차수신의사변경요청 요청,
        CancellationToken cancellationToken)
    {
        if (요청 is null || 요청.클라이언트요청Id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "배차 수신 의사 변경 요청이 올바르지 않습니다.",
                Detail = "클라이언트 요청 ID가 필요합니다."
            });
        }

        if (요청.수신의사Code is not (운영배차수신의사Code.On or 운영배차수신의사Code.Off))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "배차 수신 의사 코드가 올바르지 않습니다.",
                Detail = "On 또는 Off만 사용할 수 있습니다."
            });
        }

        return Ok(await _useCase.기사의사변경Async(CurrentDriverId(), 요청, cancellationToken));
    }

    [HttpGet("short-metrics")]
    public async Task<ActionResult<운영배차단기지표Dto>> 단기지표조회(CancellationToken cancellationToken)
        => Ok(await _useCase.단기지표조회Async(CurrentDriverId(), cancellationToken));
}
