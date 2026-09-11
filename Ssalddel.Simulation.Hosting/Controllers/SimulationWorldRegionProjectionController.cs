using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/world-stream/regions")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationWorldRegionProjectionController(
    ISimulationWorld지역ProjectionReader reader) : ControllerBase
{
    [HttpGet("{regionStableId}")]
    [ProducesResponseType(typeof(SimulationWorldRegionProjectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SimulationWorldRegionProjectionResponse>> Get(
        string regionStableId,
        CancellationToken cancellationToken)
    {
        var result = await reader.조회Async(regionStableId, cancellationToken);
        if (!result.파생Db사용가능)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: SimulationWorldRegionProjectionCodes.DatabaseDisabled);
        return result.Projection is null
            ? NotFound()
            : Ok(result.Projection);
    }
}
