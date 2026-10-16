using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Versioning;
using Ssalddel.Contracts.Common.Workflow;

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
    private readonly I운영배차제안표시Service? _presentation;

    public 기사운영배차공통Controller(I운영배차공통UseCase useCase,
        I운영배차제안표시Service? presentation = null)
    {
        _useCase = useCase;
        _presentation = presentation;
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
            return InvalidIntent("배차 수신 의사 변경 요청이 올바르지 않습니다.", "DispatchIntent.RequestIdRequired");
        }

        if (요청.수신의사Code is not (운영배차수신의사Code.On or 운영배차수신의사Code.Off))
        {
            return InvalidIntent("배차 수신 의사 코드가 올바르지 않습니다.", "DispatchIntent.InvalidCode");
        }

        if (요청.미응답자동중지해제 && 요청.수신의사Code != 운영배차수신의사Code.On)
            return InvalidIntent("신규 배차 받기를 켜서 다시 받을 수 있습니다.", "DispatchIntent.ResumeRequiresOn");

        try
        {
            return Ok(await _useCase.기사의사변경Async(CurrentDriverId(), 요청, cancellationToken));
        }
        catch (운영배차수신요청충돌Exception)
        {
            return this.To업무실패Problem("이전 요청과 내용이 달라 수신 변경을 적용하지 않았습니다. 현재 상태를 다시 확인해 주세요.",
                StatusCodes.Status409Conflict, "DispatchIntent.RequestConflict",
                업무실패분류Codes.업무규칙차단, 업무실패책임역할Codes.플랫폼운영자,
                false, 업무재시도정책Codes.자동재시도안함);
        }
    }

    [HttpPut("offers/{requestId}/receipt")]
    public async Task<IActionResult> 제안수신확인(
        string requestId,
        [FromBody] 운영배차제안표시확인요청 요청,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestId) || 요청 is null || 요청.추천라운드 <= 0)
            return InvalidIntent("배차 제안 수신 확인 요청이 올바르지 않습니다.", "DispatchReceipt.InvalidRecommendation");

        if (_presentation is null)
            return this.To업무실패Problem("배차 제안 수신 확인을 연결하지 못했습니다.",
                StatusCodes.Status503ServiceUnavailable, "DispatchReceipt.Unavailable",
                업무실패분류Codes.업무규칙차단, 업무실패책임역할Codes.플랫폼운영자,
                false, 업무재시도정책Codes.자동재시도안함);

        // 본인 인증과 현재 추천 원장을 검증합니다. 외부 요청의 기사 ID나 발생 시각을 신뢰하지 않습니다.
        var recorded = await _presentation.표시확인Async(CurrentDriverId(), requestId, 요청.추천라운드, cancellationToken);
        return recorded ? NoContent() : InvalidReceipt();
    }

    private ActionResult InvalidReceipt()
        => this.To업무실패Problem("해당 배차 제안이 바뀌었거나 종료되었습니다.",
            StatusCodes.Status409Conflict, "DispatchReceipt.RecommendationChanged",
            업무실패분류Codes.업무규칙차단, 업무실패책임역할Codes.플랫폼운영자,
            false, 업무재시도정책Codes.자동재시도안함);

    [HttpGet("short-metrics")]
    public async Task<ActionResult<운영배차단기지표Dto>> 단기지표조회(CancellationToken cancellationToken)
        => Ok(await _useCase.단기지표조회Async(CurrentDriverId(), cancellationToken));

    private ActionResult InvalidIntent(string message, string code)
        => this.To업무실패Problem(message, StatusCodes.Status400BadRequest, code,
            업무실패분류Codes.업무규칙차단, 업무실패책임역할Codes.플랫폼운영자,
            false, 업무재시도정책Codes.자동재시도안함);
}
