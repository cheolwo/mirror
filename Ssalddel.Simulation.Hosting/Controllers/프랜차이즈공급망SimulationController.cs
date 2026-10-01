using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/franchise-supply")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "프랜차이즈 공급망 조회·Preview·Confirm의 HTTP 실행 경계를 제공한다.",
    Boundary = "HTTP Adapter는 실제 공급 계약·발주·배송 권위가 아니며 Session Aggregate의 결정을 바꾸지 않는다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class 프랜차이즈공급망SimulationController(
    프랜차이즈공급망SimulationService service)
    : SimulationApiControllerBase
{
    [HttpGet]
    public ActionResult<프랜차이즈공급망StateSnapshot?> Get(
        string sessionStableId)
        => Execute<프랜차이즈공급망StateSnapshot?>(() =>
            Ok(service.Get(sessionStableId)));

    [HttpPost("initialization-previews")]
    public ActionResult<프랜차이즈공급망PreviewSnapshot>
        PreviewInitialization(string sessionStableId,
            [FromBody] 프랜차이즈공급망초기화PreviewRequest request)
        => Execute<프랜차이즈공급망PreviewSnapshot>(() =>
            Ok(service.PreviewInitialization(sessionStableId, request)));

    [HttpPost("initialization-commands")]
    public ActionResult<프랜차이즈공급망StateSnapshot>
        ConfirmInitialization(string sessionStableId,
            [FromBody] 프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈공급망초기화PreviewRequest> request)
        => Execute<프랜차이즈공급망StateSnapshot>(() =>
            Ok(service.ConfirmInitialization(sessionStableId, request)));

    [HttpPost("sourcing-agreement-previews")]
    public ActionResult<프랜차이즈공급망PreviewSnapshot>
        PreviewSourcingAgreement(string sessionStableId,
            [FromBody] 프랜차이즈조달계약PreviewRequest request)
        => Execute<프랜차이즈공급망PreviewSnapshot>(() =>
            Ok(service.PreviewSourcingAgreement(sessionStableId, request)));

    [HttpPost("sourcing-agreement-commands")]
    public ActionResult<프랜차이즈공급망StateSnapshot>
        ConfirmSourcingAgreement(string sessionStableId,
            [FromBody] 프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈조달계약PreviewRequest> request)
        => Execute<프랜차이즈공급망StateSnapshot>(() =>
            Ok(service.ConfirmSourcingAgreement(sessionStableId, request)));

    [HttpPost("store-supply-offer-previews")]
    public ActionResult<프랜차이즈공급망PreviewSnapshot>
        PreviewStoreSupplyOffer(string sessionStableId,
            [FromBody] 프랜차이즈매장공급안PreviewRequest request)
        => Execute<프랜차이즈공급망PreviewSnapshot>(() =>
            Ok(service.PreviewStoreSupplyOffer(sessionStableId, request)));

    [HttpPost("store-supply-offer-commands")]
    public ActionResult<프랜차이즈공급망StateSnapshot>
        ConfirmStoreSupplyOffer(string sessionStableId,
            [FromBody] 프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장공급안PreviewRequest> request)
        => Execute<프랜차이즈공급망StateSnapshot>(() =>
            Ok(service.ConfirmStoreSupplyOffer(sessionStableId, request)));

    [HttpPost("store-order-previews")]
    public ActionResult<프랜차이즈공급망PreviewSnapshot>
        PreviewStoreOrder(string sessionStableId,
            [FromBody] 프랜차이즈매장발주PreviewRequest request)
        => Execute<프랜차이즈공급망PreviewSnapshot>(() =>
            Ok(service.PreviewStoreOrder(sessionStableId, request)));

    [HttpPost("store-order-commands")]
    public ActionResult<프랜차이즈공급망StateSnapshot>
        ConfirmStoreOrder(string sessionStableId,
            [FromBody] 프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장발주PreviewRequest> request)
        => Execute<프랜차이즈공급망StateSnapshot>(() =>
            Ok(service.ConfirmStoreOrder(sessionStableId, request)));
}
