using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/learning-focus")]
[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E2,
    "NPC 학습중점의 읽기·미리보기·확정 HTTP Adapter를 제공한다.",
    Boundary = "HTTP Adapter는 Simulation 규칙과 운영 상태를 소유하지 않는다.")]
public sealed class SimulationPlayerLearningFocusController(
    SimulationPlayerLearningFocusService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Simulation학습중점ProjectionSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation학습중점ProjectionSnapshot> Get(
        string sessionStableId,
        [FromQuery] string playerStableId)
        => Execute(() => service.Get(sessionStableId, playerStableId));

    [HttpPost("equip/preview")]
    [ProducesResponseType(typeof(Simulation학습중점PreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation학습중점PreviewSnapshot> Preview(
        string sessionStableId,
        [FromBody] Simulation학습중점ChangeRequest request)
        => Execute(() => service.Preview(sessionStableId, request));

    [HttpPost("equip/confirm")]
    [ProducesResponseType(typeof(Simulation학습중점StateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation학습중점StateSnapshot> Confirm(
        string sessionStableId,
        [FromBody] Simulation학습중점ChangeRequest request)
        => Execute(() => service.Confirm(sessionStableId, request));

    private ActionResult<T> Execute<T>(Func<T> action)
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
