using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/player-knowledge-ledgers/{ledgerStableId}")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E4,
    "RemoteHost에서 WI-ACTOR-03 지식 조회·Preview·Confirm을 공통 Application 서비스로 전달한다.",
    Boundary = "HTTP Adapter는 처방 승인·멱등성·WorldRevision·행위 기록을 계산하지 않는다.")]
public sealed class SimulationPlayerKnowledgeController(
    Simulation플레이어지식Service service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Simulation플레이어지식LedgerSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation플레이어지식LedgerSnapshot> Get(
        string ledgerStableId)
        => Execute(() => service.Get(ledgerStableId));

    [HttpPost("previews")]
    [ProducesResponseType(typeof(Simulation지식습득PreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation지식습득PreviewSnapshot> Preview(
        string ledgerStableId,
        [FromBody] Simulation지식습득PreviewRequest request)
        => Execute(() => service.Preview(ledgerStableId, request));

    [HttpPost("confirmations")]
    [ProducesResponseType(typeof(Simulation지식습득ConfirmResult),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation지식습득ConfirmResult> Confirm(
        string ledgerStableId,
        [FromBody] Simulation지식습득ConfirmRequest request)
        => Execute(() => service.Confirm(ledgerStableId, request));

    private ActionResult<T> Execute<T>(Func<T> action)
    {
        try
        {
            return Ok(action());
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse
            {
                ErrorCode = error.ErrorCode,
            });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse
            {
                ErrorCode = error.ErrorCode,
            });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse
            {
                ErrorCode = error.ErrorCode,
            });
        }
    }
}
