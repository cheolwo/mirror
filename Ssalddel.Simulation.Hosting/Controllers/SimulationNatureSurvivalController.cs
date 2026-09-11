using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/nature-survival")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class SimulationNatureSurvivalController(
    SimulationNatureSurvivalService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationNatureSurvivalStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNatureSurvivalStateSnapshot> Get(string sessionStableId)
        => Execute(() => service.Get(sessionStableId));

    [HttpGet("observation")]
    [ProducesResponseType(typeof(SimulationNature표현관측Snapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNature표현관측Snapshot> GetObservation(
        string sessionStableId)
        => Execute(() => service.GetObservation(sessionStableId));

    [HttpGet("player-opportunities")]
    [ProducesResponseType(typeof(Simulation플레이어기회Snapshot[]),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation플레이어기회Snapshot[]> GetPlayerOpportunities(
        string sessionStableId)
        => Execute(() => service.GetPlayerOpportunities(sessionStableId));

    [HttpGet("area-needs")]
    [ProducesResponseType(typeof(Simulation영역수요Snapshot[]),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation영역수요Snapshot[]> GetAreaNeeds(
        string sessionStableId)
        => Execute(() => service.GetAreaNeeds(sessionStableId));

    [HttpGet("building-progression/{areaCode}")]
    [ProducesResponseType(typeof(Simulation영역건물발전Snapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation영역건물발전Snapshot> GetBuildingProgression(
        string sessionStableId, string areaCode)
        => Execute(() => service.GetBuildingProgression(sessionStableId, areaCode));

    [HttpPost("previews")]
    [ProducesResponseType(typeof(SimulationNatureSurvivalActionPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNatureSurvivalActionPreviewSnapshot> Preview(
        string sessionStableId,
        [FromBody] SimulationNatureSurvivalActionPreviewRequest request)
        => Execute(() => service.Preview(sessionStableId, request));

    [HttpPost("commands")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Confirm(
        string sessionStableId,
        [FromBody] SimulationNatureSurvivalCommandRequest request)
        => Execute(() => service.Confirm(sessionStableId, request));

    [HttpPost("clock/advance")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> AdvanceClock(
        string sessionStableId,
        [FromBody] SimulationNatureSurvivalClockAdvanceRequest request)
        => Execute(() => service.AdvanceClock(sessionStableId, request));

    [HttpPost("focus-timing/attempts")]
    [ProducesResponseType(typeof(Simulation집중판정ChallengeSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation집중판정ChallengeSnapshot> SubmitFocusTiming(
        string sessionStableId,
        [FromBody] Simulation집중판정AttemptRequest request)
        => Execute(() => service.SubmitFocusTiming(sessionStableId, request));

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
