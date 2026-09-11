using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/survival-tarot")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationSurvivalTarotController(
    SimulationSurvivalTarotService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationSurvivalTarotStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationSurvivalTarotStateSnapshot> Get(string sessionStableId)
        => Execute(() => service.Get(sessionStableId));

    [HttpPost("responses/confirm")]
    [ProducesResponseType(typeof(SimulationSurvivalTarotCommandResultSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationSurvivalTarotCommandResultSnapshot> ConfirmResponse(
        string sessionStableId,
        [FromBody] SimulationSurvivalTarotResponseConfirmRequest request)
        => Execute(() => service.ConfirmResponse(sessionStableId, request));

    [HttpPost("resolutions/confirm")]
    [ProducesResponseType(typeof(SimulationSurvivalTarotCommandResultSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationSurvivalTarotCommandResultSnapshot> ConfirmResolution(
        string sessionStableId,
        [FromBody] SimulationSurvivalTarotResolutionConfirmRequest request)
        => Execute(() => service.ConfirmResolution(sessionStableId, request));

    private ActionResult<T> Execute<T>(Func<T> action)
    {
        try
        {
            return Ok(action());
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
