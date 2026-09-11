using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/actor-equipment")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "원격 Host에서 보편 물품 획득·장착 계약을 같은 Simulation Core로 전달한다.",
    Boundary = "HTTP Adapter는 장착 결과나 능력을 자체 계산하지 않는다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class SimulationActorEquipmentController(
    SimulationActorEquipmentService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<SimulationActorEquipmentStateSnapshot> Get(
        string sessionStableId)
        => Execute(() => service.Get(sessionStableId));

    [HttpPost("item-acquisition-previews")]
    public ActionResult<SimulationActorItemAcquirePreviewSnapshot> PreviewAcquire(
        string sessionStableId,
        [FromBody] SimulationActorItemAcquirePreviewRequest request)
        => Execute(() => service.PreviewAcquire(sessionStableId, request));

    [HttpPost("item-acquisitions/confirm")]
    public ActionResult<SimulationActorEquipmentStateSnapshot> ConfirmAcquire(
        string sessionStableId,
        [FromBody] SimulationActorItemAcquireConfirmRequest request)
        => Execute(() => service.ConfirmAcquire(sessionStableId, request));

    [HttpPost("change-previews")]
    public ActionResult<SimulationActorEquipmentChangePreviewSnapshot> PreviewChange(
        string sessionStableId,
        [FromBody] SimulationActorEquipmentChangePreviewRequest request)
        => Execute(() => service.PreviewChange(sessionStableId, request));

    [HttpPost("changes/confirm")]
    public ActionResult<SimulationActorEquipmentStateSnapshot> ConfirmChange(
        string sessionStableId,
        [FromBody] SimulationActorEquipmentChangeConfirmRequest request)
        => Execute(() => service.ConfirmChange(sessionStableId, request));

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
