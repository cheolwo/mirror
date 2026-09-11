using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationSessionLifecycle,
    SsalddelCodeLayer.Api,
    "세션 생성·조회·Tick HTTP 경계를 제공한다.",
    StepKey = "api.session-lifecycle",
    DependsOnStepKeys = new string[] { "contract.session-create" },
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.StateMutation,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 20,
    Boundary = "Simulation 실행 모드에서만 조립되며 오류 계약과 기존 route를 보존한다.")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationSaveReplay,
    SsalddelCodeLayer.Api,
    "세션 저장·복원 HTTP 경계를 제공한다.",
    StepKey = "api.save-replay",
    DependsOnStepKeys = new string[] { "contract.save-request" },
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 20,
    Boundary = "저장 식별자와 기대 개정을 서버가 검증하며 운영 서버 저장 API로 전달하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class 경영SimulationSessionsController(
    경영SimulationSessionService service) : SimulationApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status201Created)]
    public ActionResult<경영SimulationSessionSnapshot> Create(
        [FromBody] 경영SimulationSession생성Request request)
        => Execute<경영SimulationSessionSnapshot>(() =>
        {
            var result = service.Create(request);
            return CreatedAtAction(nameof(Get), new { sessionStableId = result.SessionStableId }, result);
        });

    [HttpGet("{sessionStableId}")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Get(string sessionStableId)
        => Execute<경영SimulationSessionSnapshot>(() => Ok(service.Get(sessionStableId)));

    [HttpPost("{sessionStableId}/ticks")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Advance(
        string sessionStableId,
        [FromBody] 경영SimulationTick진행Request request)
        => Execute<경영SimulationSessionSnapshot>(() => Ok(service.Advance(sessionStableId, request)));

    [HttpPost("{sessionStableId}/saves")]
    [ProducesResponseType(typeof(SimulationSessionSavePackage), StatusCodes.Status200OK)]
    public ActionResult<SimulationSessionSavePackage> Save(
        string sessionStableId,
        [FromBody] SimulationSessionSaveRequest request)
        => Execute<SimulationSessionSavePackage>(() => Ok(service.Save(sessionStableId, request)));

    [HttpPost("restores")]
    [ProducesResponseType(typeof(SimulationSessionRestoreResult), StatusCodes.Status200OK)]
    public ActionResult<SimulationSessionRestoreResult> Restore(
        [FromBody] SimulationSessionRestoreRequest request)
        => Execute<SimulationSessionRestoreResult>(() => Ok(service.Restore(request)));

    [HttpPost("replay-verifications")]
    [ProducesResponseType(typeof(SimulationSessionRestoreResult), StatusCodes.Status200OK)]
    public ActionResult<SimulationSessionRestoreResult> VerifyReplay(
        [FromBody] SimulationSessionRestoreRequest request)
        => Execute<SimulationSessionRestoreResult>(() =>
            Ok(service.VerifyReplay(request)));

    // 기존 직접 호출 시험과 내부 도구의 source 호환을 위한 비-API forwarding surface다.
    [NonAction]
    public ActionResult<SimulationTurnClosingContextSnapshot> GetTurnClosingContext(
        string sessionStableId)
        => Ok(service.GetTurnClosingContext(sessionStableId));

    [NonAction]
    public ActionResult<SimulationTurnClosingPreviewSnapshot> PreviewTurnClosing(
        string sessionStableId,
        SimulationTurnClosingPreviewRequest request)
        => Ok(service.PreviewTurnClosing(sessionStableId, request));

    [NonAction]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmTurnClosing(
        string sessionStableId,
        SimulationTurnClosingConfirmRequest request)
        => Ok(service.ConfirmTurnClosing(sessionStableId, request));

}
