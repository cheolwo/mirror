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
public sealed class 경영Simulation물류창고Controller(
    경영Simulation물류창고Service service) : ControllerBase
{
    [HttpPost("{sessionStableId}/logistics-movement-previews")]
    [ProducesResponseType(typeof(SimulationLogisticsMovementPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationLogisticsMovementPreviewSnapshot> PreviewLogisticsMovement(
        string sessionStableId,
        [FromBody] SimulationLogisticsMovementPreviewRequest request)
        => Ok(service.PreviewLogisticsMovement(sessionStableId, request));

    [HttpPost("{sessionStableId}/logistics-movements/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmLogisticsMovement(
        string sessionStableId,
        [FromBody] SimulationLogisticsMovementConfirmRequest request)
        => Ok(service.ConfirmLogisticsMovement(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-dispatch-previews")]
    [ProducesResponseType(typeof(SimulationFreightDispatchPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationFreightDispatchPreviewSnapshot> PreviewFreightDispatch(
        string sessionStableId,
        [FromBody] SimulationFreightDispatchPreviewRequest request)
        => Ok(service.PreviewFreightDispatch(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-dispatches/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmFreightDispatch(
        string sessionStableId,
        [FromBody] SimulationFreightDispatchConfirmRequest request)
        => Ok(service.ConfirmFreightDispatch(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-transport-previews")]
    [ProducesResponseType(typeof(SimulationFreightTransportPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationFreightTransportPreviewSnapshot> PreviewFreightTransport(
        string sessionStableId,
        [FromBody] SimulationFreightTransportPreviewRequest request)
        => Ok(service.PreviewFreightTransport(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-transports/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmFreightTransport(
        string sessionStableId,
        [FromBody] SimulationFreightTransportConfirmRequest request)
        => Ok(service.ConfirmFreightTransport(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-receipt-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewFreightReceipt(
        string sessionStableId,
        [FromBody] SimulationFreightReceiptPreviewRequest request)
        => Ok(service.PreviewFreightReceipt(sessionStableId, request));

    [HttpPost("{sessionStableId}/freight-receipts/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmFreightReceipt(
        string sessionStableId,
        [FromBody] SimulationFreightReceiptConfirmRequest request)
        => Ok(service.ConfirmFreightReceipt(sessionStableId, request));

    [HttpPost("{sessionStableId}/warehouse-put-away-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewWarehousePutAway(
        string sessionStableId,
        [FromBody] SimulationWarehousePutAwayPreviewRequest request)
        => Ok(service.PreviewWarehousePutAway(sessionStableId, request));

    [HttpPost("{sessionStableId}/warehouse-put-aways/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmWarehousePutAway(
        string sessionStableId,
        [FromBody] SimulationWarehousePutAwayConfirmRequest request)
        => Ok(service.ConfirmWarehousePutAway(sessionStableId, request));

    [HttpPost("{sessionStableId}/supply-chain-work-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewSupplyChainWork(
        string sessionStableId,
        [FromBody] SimulationSupplyChainWorkPreviewRequest request)
        => Ok(service.PreviewSupplyChainWork(sessionStableId, request));

    [HttpPost("{sessionStableId}/supply-chain-works/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmSupplyChainWork(
        string sessionStableId,
        [FromBody] SimulationSupplyChainWorkConfirmRequest request)
        => Ok(service.ConfirmSupplyChainWork(sessionStableId, request));

    [HttpGet("/api/simulation/v1/sessions/{sessionStableId}/npc-routine-work")]
    public ActionResult<SimulationNpcRoutineWorkProjection[]> GetNpcRoutineWork(
        string sessionStableId,
        [FromQuery] string areaCode = "Hub")
        => Ok(service.GetNpcRoutineWork(sessionStableId, areaCode));

    [HttpGet("/api/simulation/v1/sessions/{sessionStableId}/spatial-composition")]
    public ActionResult<SimulationSpatialCompositionStateSnapshot>
        GetSpatialComposition(
            string sessionStableId,
            [FromQuery] string areaCode = "Hub")
        => Ok(service.GetSpatialComposition(sessionStableId, areaCode));
}
