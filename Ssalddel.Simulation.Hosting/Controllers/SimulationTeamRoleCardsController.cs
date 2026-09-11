using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/team-role-cards")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationTeamRoleCardsController(
    SimulationTeamRoleCardService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationTeamRoleCardStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTeamRoleCardStateSnapshot> Get(
        string sessionStableId,
        [FromQuery] string actorStableId)
        => Execute(() => service.Get(sessionStableId, actorStableId));

    [HttpPost("equip")]
    [ProducesResponseType(typeof(SimulationTeamRoleCardStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTeamRoleCardStateSnapshot> Equip(
        string sessionStableId,
        [FromBody] SimulationTeamRoleCardEquipRequest request)
        => Execute(() => service.Equip(sessionStableId, request));

    [HttpPost("activities/start")]
    [ProducesResponseType(typeof(SimulationTeamRoleCardStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTeamRoleCardStateSnapshot> StartActivity(
        string sessionStableId,
        [FromBody] SimulationTeamActivityStartRequest request)
        => Execute(() => service.StartActivity(sessionStableId, request));

    [HttpPost("activities/end")]
    [ProducesResponseType(typeof(SimulationTeamRoleCardStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTeamRoleCardStateSnapshot> EndActivity(
        string sessionStableId,
        [FromBody] SimulationTeamActivityEndRequest request)
        => Execute(() => service.EndActivity(sessionStableId, request));

    [HttpPost("combat-loadouts/set")]
    [ProducesResponseType(typeof(SimulationTeamRoleCardStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationTeamRoleCardStateSnapshot> SetCombatLoadout(
        string sessionStableId,
        [FromBody] SimulationCombatCardLoadoutSetRequest request)
        => Execute(() => service.SetCombatLoadout(sessionStableId, request));

    private ActionResult<SimulationTeamRoleCardStateSnapshot> Execute(
        Func<SimulationTeamRoleCardStateSnapshot> action)
    {
        try
        {
            return Ok(action());
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
    }
}
