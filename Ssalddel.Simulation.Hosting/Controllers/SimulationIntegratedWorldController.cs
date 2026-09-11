using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/integrated-world")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationIntegratedWorldController(
    경영Simulation통합생활세계Service service) : ControllerBase
{
    [HttpPost("previews")]
    [ProducesResponseType(typeof(SimulationIntegratedWorldPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationIntegratedWorldPreviewSnapshot> Preview(
        string sessionStableId,
        [FromBody] SimulationIntegratedWorldCommandRequest request)
        => Execute<SimulationIntegratedWorldPreviewSnapshot>(
            () => Ok(service.Preview(sessionStableId, request)));

    [HttpPost("commands")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Confirm(
        string sessionStableId,
        [FromBody] SimulationIntegratedWorldCommandRequest request)
        => Execute<경영SimulationSessionSnapshot>(
            () => Ok(service.Confirm(sessionStableId, request)));

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
