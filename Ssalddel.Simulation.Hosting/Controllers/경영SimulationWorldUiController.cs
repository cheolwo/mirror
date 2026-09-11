using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class 경영SimulationWorldUiController(
    SimulationWorldUIProjectionService projectionService) : ControllerBase
{
    [HttpGet("{sessionStableId}/world-ui/surfaces/{surfaceStableId}")]
    [ProducesResponseType(typeof(SimulationWorldUIProjection), StatusCodes.Status200OK)]
    public ActionResult<SimulationWorldUIProjection> GetWorldUiSurface(
        string sessionStableId,
        string surfaceStableId)
        => Ok(projectionService.Get(sessionStableId, surfaceStableId));
}
