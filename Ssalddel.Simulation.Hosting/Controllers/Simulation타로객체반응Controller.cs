using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class Simulation타로객체반응Controller(
    Simulation타로객체반응PreviewService service) : ControllerBase
{
    [HttpPost("{sessionStableId}/tarot-object-reaction-previews")]
    [ProducesResponseType(
        typeof(Simulation타로객체반응PreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation타로객체반응PreviewSnapshot> Preview(
        string sessionStableId,
        [FromBody] Simulation타로객체반응PreviewRequest request)
    {
        try
        {
            return Ok(service.Preview(sessionStableId, request));
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
