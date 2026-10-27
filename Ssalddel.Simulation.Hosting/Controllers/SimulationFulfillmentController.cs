using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

/// <summary>기존 sessions 접근 필터 안에서 동일한 Simulation 원장을 조회·확정한다.</summary>
[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/fulfillment")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "출고 센터·위치·담당 선택을 Simulation HTTP 경계로 전달한다.",
    Boundary = "세션 접근 권한과 Simulation 상태 권위를 유지하며 운영 재고를 변경하지 않는다.")]
public sealed class SimulationFulfillmentController(경영Simulation물류창고Service service)
    : SimulationApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationFulfillmentStateSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<SimulationFulfillmentStateSnapshot> Get(string sessionStableId)
        => Execute<SimulationFulfillmentStateSnapshot>(() =>
        {
            var state = service.GetFulfillmentState(sessionStableId);
            return state is null ? NoContent() : Ok(state);
        });

    [HttpPost("previews")]
    [ProducesResponseType(typeof(SimulationFulfillmentPlanSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationFulfillmentPlanSnapshot> Preview(
        string sessionStableId, [FromBody] SimulationFulfillmentOrderRequest request)
        => Execute<SimulationFulfillmentPlanSnapshot>(() =>
            Ok(service.PreviewFulfillment(sessionStableId, request)));

    [HttpPost("confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Confirm(
        string sessionStableId, [FromBody] SimulationFulfillmentCommand request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmFulfillment(sessionStableId, request)));
}
