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
public sealed class 경영Simulation주문소비Controller(
    경영Simulation주문소비Service service) : ControllerBase
{
    [HttpPost("{sessionStableId}/group-order-previews")]
    [ProducesResponseType(typeof(Simulation같이주문PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation같이주문PreviewSnapshot> PreviewGroupOrder(
        string sessionStableId,
        [FromBody] Simulation같이주문PreviewRequest request)
        => Ok(service.PreviewGroupOrder(sessionStableId, request));

    [HttpPost("{sessionStableId}/group-orders/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmGroupOrder(
        string sessionStableId,
        [FromBody] Simulation같이주문ConfirmRequest request)
        => Ok(service.ConfirmGroupOrder(sessionStableId, request));

    [HttpPost("{sessionStableId}/restaurant-responses/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmRestaurantResponse(
        string sessionStableId,
        [FromBody] Simulation음식점응답Request request)
        => Ok(service.ConfirmRestaurantResponse(sessionStableId, request));

    [HttpPost("{sessionStableId}/food-delivery-previews")]
    [ProducesResponseType(typeof(Simulation음식배달PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation음식배달PreviewSnapshot> PreviewFoodDelivery(
        string sessionStableId,
        [FromBody] Simulation음식배달PreviewRequest request)
        => Ok(service.PreviewFoodDelivery(sessionStableId, request));

    [HttpPost("{sessionStableId}/food-deliveries/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmFoodDelivery(
        string sessionStableId,
        [FromBody] Simulation음식배달ConfirmRequest request)
        => Ok(service.ConfirmFoodDelivery(sessionStableId, request));

    [HttpPost("{sessionStableId}/food-delivery-receipt-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceipt(
        string sessionStableId,
        [FromBody] Simulation음식배달수령PreviewRequest request)
        => Ok(service.PreviewFoodDeliveryReceipt(sessionStableId, request));

    [HttpPost("{sessionStableId}/food-delivery-receipts/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceipt(
        string sessionStableId,
        [FromBody] Simulation음식배달수령ConfirmRequest request)
        => Ok(service.ConfirmFoodDeliveryReceipt(sessionStableId, request));

    [HttpPost("{sessionStableId}/market-consumption-previews")]
    [ProducesResponseType(typeof(Simulation시장소비PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation시장소비PreviewSnapshot> PreviewMarketConsumption(
        string sessionStableId,
        [FromBody] Simulation시장소비PreviewRequest request)
        => Ok(service.PreviewMarketConsumption(sessionStableId, request));

    [HttpPost("{sessionStableId}/market-consumptions/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmMarketConsumption(
        string sessionStableId,
        [FromBody] Simulation시장소비ConfirmRequest request)
        => Ok(service.ConfirmMarketConsumption(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-order-previews")]
    [ProducesResponseType(typeof(SimulationIndividualOrderPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationIndividualOrderPreviewSnapshot> PreviewIndividualOrder(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderPreviewRequest request)
        => Ok(service.PreviewIndividualOrder(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-orders/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmIndividualOrder(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderConfirmRequest request)
        => Ok(service.ConfirmIndividualOrder(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-order-pickup-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewIndividualOrderPickup(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderPickupPreviewRequest request)
        => Ok(service.PreviewIndividualOrderPickup(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-order-pickups/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmIndividualOrderPickup(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderPickupConfirmRequest request)
        => Ok(service.ConfirmIndividualOrderPickup(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-order-cancellation-previews")]
    [ProducesResponseType(typeof(SimulationDecisionPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationDecisionPreviewSnapshot> PreviewIndividualOrderCancellation(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderCancelRequest request)
        => Ok(service.PreviewIndividualOrderCancellation(sessionStableId, request));

    [HttpPost("{sessionStableId}/individual-order-cancellations/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmIndividualOrderCancellation(
        string sessionStableId,
        [FromBody] SimulationIndividualOrderCancelRequest request)
        => Ok(service.ConfirmIndividualOrderCancellation(sessionStableId, request));
}
