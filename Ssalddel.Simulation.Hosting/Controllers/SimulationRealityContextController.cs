using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/reality-context")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E6,
    "세계 의미·인과·근거와 플레이 준비도 책임을 제공한다.",
    Boundary = "운영 근거와 Simulation 규칙 및 E 승격을 분리한다.")]
public sealed class SimulationRealityContextController(
    SimulationRealityContextService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationRealityContextPlayerProjectionResponse),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationRealityContextPlayerProjectionResponse> Get(
        string sessionStableId,
        [FromQuery] bool includeSourceDetails = false)
    {
        try
        {
            return Ok(service.ReadPlayerProjection(sessionStableId,
                includeSourceDetails));
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
