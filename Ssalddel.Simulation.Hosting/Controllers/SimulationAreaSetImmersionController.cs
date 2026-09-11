using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/world-stream/area-sets")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E6,
    "AreaSet 플레이 전 세계 정제 결과를 조회한다.",
    Boundary = "HTTP 조회 결과는 저장 Scene·Play Mode·Game View 증거가 아니다.")]
public sealed class SimulationAreaSetImmersionController(
    SimulationAreaSetImmersionService service) : ControllerBase
{
    [HttpGet("{areaSetStableId}/immersion-readiness")]
    [ProducesResponseType(typeof(SimulationAreaSetImmersionReadinessResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationAreaSetImmersionReadinessResponse>> Get(
        string areaSetStableId, CancellationToken cancellationToken)
    {
        var result = await service.ReadAsync(areaSetStableId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }
}
