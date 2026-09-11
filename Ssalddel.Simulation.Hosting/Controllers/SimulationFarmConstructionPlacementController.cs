using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/integrated-world/farm-construction-placement")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E4,
    "공간 WI의 실행 문맥·세계 발현 판정에 사용할 공간 조립 증거를 제공한다.",
    Boundary = "AreaSet·Graph·배치·통행은 조건부 입력이며 그 자체로 E4·E5를 완료하지 않는다.")]
public sealed class SimulationFarmConstructionPlacementController(
    경영Simulation통합생활세계Service service) : ControllerBase
{
    [HttpPost("previews")]
    [ProducesResponseType(typeof(SimulationFarmConstructionPlacementPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmConstructionPlacementPreviewSnapshot> Preview(
        string sessionStableId,
        [FromBody] SimulationFarmConstructionPlacementPreviewRequest request)
        => Execute<SimulationFarmConstructionPlacementPreviewSnapshot>(
            () => Ok(service.PreviewFarmPlacement(sessionStableId, request)));

    [HttpPost("commands")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Confirm(
        string sessionStableId,
        [FromBody] SimulationFarmConstructionPlacementConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(
            () => Ok(service.ConfirmFarmPlacement(sessionStableId, request)));

    private ActionResult<T> Execute<T>(Func<ActionResult<T>> action)
    {
        try { return action(); }
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
