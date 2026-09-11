using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/idea-map")]
[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E2,
    "플레이어 이데아 맵의 읽기 전용 HTTP Adapter를 제공한다.",
    Boundary = "HTTP Adapter는 관계를 생성하거나 Simulation 권위 상태를 변경하지 않는다.")]
public sealed class SimulationPlayerIdeaMapController(
    SimulationPlayerIdeaMapService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Simulation플레이어이데아맵ProjectionSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation플레이어이데아맵ProjectionSnapshot> Get(
        string sessionStableId, [FromQuery] string playerStableId)
    {
        try
        {
            return Ok(service.Get(sessionStableId, playerStableId));
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
    }
}
