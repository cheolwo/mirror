using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/battles")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationParallelBattle,
    SsalddelCodeLayer.Api,
    "병렬 전투 조회·Preview·Confirm·진행 HTTP 경계를 제공한다.",
    StepKey = "api.battle",
    DependsOnStepKeys = new string[] { "contract.battle-preview" },
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.StateMutation,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 20,
    Boundary = "클라이언트가 보낸 안정 ID와 예상 개정만 받아 서버 규칙으로 전투 상태를 확정한다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationBattlesController(SimulationBattleInstanceService service)
    : ControllerBase
{
    [HttpGet]
    public ActionResult<SimulationBattleInstanceSnapshot[]> List(string sessionStableId,
        [FromQuery] string actorStableId)
        => Execute(() => service.List(sessionStableId, actorStableId));

    [HttpGet("{battleStableId}")]
    public ActionResult<SimulationBattleInstanceSnapshot> Get(string sessionStableId,
        string battleStableId, [FromQuery] string actorStableId)
        => Execute(() => service.Get(sessionStableId, battleStableId, actorStableId));

    [HttpPost("previews")]
    public ActionResult<SimulationBattleCreatePreviewSnapshot> PreviewCreate(
        string sessionStableId, [FromBody] SimulationBattleCreatePreviewRequest request)
        => Execute(() => service.PreviewCreate(sessionStableId, request));

    [HttpPost("confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmCreate(
        string sessionStableId, [FromBody] SimulationBattleCreateConfirmRequest request)
        => Execute(() => service.ConfirmCreate(sessionStableId, request));

    [HttpPost("{battleStableId}/participants/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmParticipation(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleParticipationConfirmRequest request)
        => Execute(() => service.ConfirmParticipation(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/deployments/preview")]
    public ActionResult<SimulationBattleDeploymentPreviewSnapshot> PreviewDeployment(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleDeploymentPreviewRequest request)
        => Execute(() => service.PreviewDeployment(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/deployments/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmDeployment(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleDeploymentConfirmRequest request)
        => Execute(() => service.ConfirmDeployment(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/support-previews")]
    public ActionResult<SimulationBattleSupportPreviewSnapshot> PreviewSupport(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleSupportPreviewRequest request)
        => Execute(() => service.PreviewSupport(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/supports/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmSupport(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleSupportConfirmRequest request)
        => Execute(() => service.ConfirmSupport(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/ticks")]
    public ActionResult<SimulationBattleInstanceSnapshot> Advance(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleAdvanceRequest request)
        => Execute(() => service.Advance(sessionStableId, battleStableId, request));

    [HttpPost("{battleStableId}/commands/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmTacticalCommand(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleTacticalCommandConfirmRequest request)
        => Execute(() => service.ConfirmTacticalCommand(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/local-focus/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmLocalFocus(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationLocalCombatFocusConfirmRequest request)
        => Execute(() => service.ConfirmLocalFocus(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/local-actions/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmLocalAction(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationLocalCombatActionConfirmRequest request)
        => Execute(() => service.ConfirmLocalAction(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/local-control-mode/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmLocalControlMode(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationLocalCombatControlModeConfirmRequest request)
        => Execute(() => service.ConfirmLocalControlMode(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/observer-interventions/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmObserverIntervention(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationLocalCombatObserverInterventionConfirmRequest request)
        => Execute(() => service.ConfirmObserverIntervention(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/escalations/preview")]
    public ActionResult<SimulationBattleEscalationPreviewSnapshot> PreviewEscalation(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleEscalationPreviewRequest request)
        => Execute(() => service.PreviewEscalation(sessionStableId,
            battleStableId, request));

    [HttpPost("{battleStableId}/escalations/confirm")]
    public ActionResult<SimulationBattleInstanceSnapshot> ConfirmEscalation(
        string sessionStableId, string battleStableId,
        [FromBody] SimulationBattleEscalationConfirmRequest request)
        => Execute(() => service.ConfirmEscalation(sessionStableId,
            battleStableId, request));

    private ActionResult<T> Execute<T>(Func<T> action)
    {
        try { return Ok(action()); }
        catch (SimulationContractException error)
        { return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode }); }
        catch (SimulationNotFoundException error)
        { return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode }); }
        catch (SimulationConflictException error)
        { return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode }); }
    }
}
