using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/farm-survival")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationFarmCombatInput,
    SsalddelCodeLayer.Api,
    "전투 시점·박자 시작·반응 확정 HTTP 경계를 제공한다.",
    StepKey = "api.farm-combat",
    DependsOnStepKeys = new[] { "contract.farm-combat" },
    FlowOrder = 20,
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    Effects = SsalddelCodeEffect.StateMutation,
    Boundary = "Simulation 전용 경로이며 운영 서버 권한·원장을 변경하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationFarmSurvivalController(
    SimulationFarmSurvivalService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> Get(
        string sessionStableId)
        => Execute(() => service.Get(sessionStableId));

    [HttpPost("work/preview")]
    [ProducesResponseType(typeof(SimulationFarmWorkPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmWorkPreviewSnapshot> PreviewWork(
        string sessionStableId,
        [FromBody] SimulationFarmWorkPreviewRequest request)
        => Execute(() => service.PreviewWork(sessionStableId, request));

    [HttpPost("work/confirm")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmWork(
        string sessionStableId,
        [FromBody] SimulationFarmWorkConfirmRequest request)
        => Execute(() => service.ConfirmWork(sessionStableId, request));

    [HttpPost("work-plans/preview")]
    [ProducesResponseType(typeof(SimulationFarmWorkPlanPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmWorkPlanPreviewSnapshot> PreviewWorkPlan(
        string sessionStableId,
        [FromBody] SimulationFarmWorkPlanPreviewRequest request)
        => Execute(() => service.PreviewWorkPlan(sessionStableId, request));

    [HttpPost("work-plans/confirm")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmWorkPlan(
        string sessionStableId,
        [FromBody] SimulationFarmWorkPlanConfirmRequest request)
        => Execute(() => service.ConfirmWorkPlan(sessionStableId, request));

    [HttpPost("threat-responses/confirm")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmThreatResponse(
        string sessionStableId,
        [FromBody] SimulationThreatResponseConfirmRequest request)
        => Execute(() => service.ConfirmThreatResponse(sessionStableId, request));

    [HttpPost("combat/perspective/confirm")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmCombatPerspective(
        string sessionStableId,
        [FromBody] SimulationCombatPerspectiveConfirmRequest request)
        => Execute(() => service.ConfirmCombatPerspective(sessionStableId, request));

    [HttpPost("combat/beats/start")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> StartCombatBeat(
        string sessionStableId,
        [FromBody] SimulationCombatBeatStartRequest request)
        => Execute(() => service.StartCombatBeat(sessionStableId, request));

    [HttpPost("combat/beats/{beatStableId}/react")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmCombatReaction(
        string sessionStableId,
        string beatStableId,
        [FromBody] SimulationCombatReactionConfirmRequest request)
    {
        if (request == null || !string.Equals(beatStableId, request.BeatStableId,
            StringComparison.Ordinal))
            return BadRequest(new SimulationErrorResponse
            {
                ErrorCode = "SimulationCombatBeatRouteMismatch",
            });
        return Execute(() => service.ConfirmCombatReaction(sessionStableId, request));
    }

    [HttpPost("combat/tactical-orders/preview")]
    [ProducesResponseType(typeof(SimulationTacticalOrderPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTacticalOrderPreviewSnapshot> PreviewTacticalOrder(
        string sessionStableId,
        [FromBody] SimulationTacticalOrderPreviewRequest request)
        => Execute(() => service.PreviewTacticalOrder(sessionStableId, request));

    [HttpPost("combat/tactical-orders/{orderWindowStableId}/confirm")]
    [ProducesResponseType(typeof(SimulationFarmSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmSurvivalStateSnapshot> ConfirmTacticalOrder(
        string sessionStableId,
        string orderWindowStableId,
        [FromBody] SimulationTacticalOrderConfirmRequest request)
    {
        if (request == null || !string.Equals(orderWindowStableId,
            request.OrderWindowStableId, StringComparison.Ordinal))
            return BadRequest(new SimulationErrorResponse
            {
                ErrorCode = "SimulationTacticalOrderWindowRouteMismatch",
            });
        return Execute(() => service.ConfirmTacticalOrder(sessionStableId, request));
    }

    private ActionResult<T> Execute<T>(Func<T> action)
    {
        try
        {
            return Ok(action());
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
