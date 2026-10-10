using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.공통;
namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelApiAudience(SsalddelActor.WarehouseManager)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiAudience(SsalddelActor.Orderer)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Decide)]
[Route(DistributedFulfillmentCodes.BaseRoute)]
[SsalddelApiContractName("DistributedFulfillmentController")]
public sealed class DistributedFulfillmentController(IDistributedFulfillmentUseCase useCase) : ControllerBase
{
    [HttpGet("workspace")]
    public Task<IActionResult> Workspace([FromQuery] string roleKey = "warehouse", CancellationToken ct = default) => Run(a => useCase.WorkspaceAsync(a, roleKey, ct));
    [HttpGet("sites/{id}/inventory")]
    public Task<IActionResult> 공간재고(string id, [FromQuery] string roleKey = "warehouse", [FromQuery] int skip = 0,
        [FromQuery] int take = 50, CancellationToken ct = default) => Run(a => useCase.공간재고Async(a, id, roleKey, skip, take, ct));
    [HttpGet("world-projection")]
    public Task<IActionResult> World([FromQuery] string roleKey = "warehouse", CancellationToken ct = default) => Run(a => useCase.WorldAsync(a, roleKey, ct));
    [HttpGet("waves/{id}")]
    public Task<IActionResult> Wave(string id, [FromQuery] string roleKey = "warehouse", CancellationToken ct = default) => Run(a => useCase.WaveAsync(a, id, roleKey, ct));
    [HttpGet("receipts/{requestId:guid}")]
    public Task<IActionResult> Receipt(Guid requestId, CancellationToken ct) => Run(a => useCase.ReceiptAsync(a, requestId, ct));
    [HttpGet("waves/{id}/order-ledger-candidates")]
    public Task<IActionResult> RootOrderCandidates(string id, [FromQuery] string cargoRequestId,
        [FromQuery] string roleKey = "shipper", CancellationToken ct = default)
        => Run(a => useCase.RootOrderCandidatesAsync(a, id, cargoRequestId, roleKey, ct));
    [HttpPost("sites")]
    public Task<IActionResult> CreateSite(DistributedSiteCreateRequest r, CancellationToken ct) => Run(a => useCase.SiteAsync(a, r, ct));
    [HttpPost("sites/{id}/consents")]
    public Task<IActionResult> Consent(string id, DistributedConsentRequest r, CancellationToken ct) => Run(a => useCase.ConsentAsync(a, id, r, ct));
    [HttpPost("sites/{id}/storage-agreements")]
    public Task<IActionResult> Agreement(string id, DistributedStorageAgreementRequest r, CancellationToken ct) => Run(a => useCase.AgreementAsync(a, id, r, ct));
    [HttpPost("storage-agreements/{id}/terms")]
    public Task<IActionResult> AgreementTerms(string id, DistributedStorageAgreementRequest r, CancellationToken ct) => Run(a => useCase.AgreementTermsAsync(a, id, r, ct));
    [HttpPost("storage-agreements/{id}/consents")]
    public Task<IActionResult> AgreementConsent(string id, DistributedStorageAgreementConsentRequest r, CancellationToken ct) => Run(a => useCase.AgreementConsentAsync(a, id, r, ct));
    [HttpPost("sites/{id}/locations")]
    public Task<IActionResult> Location(string id, DistributedLocationCreateRequest r, CancellationToken ct) => Run(a => useCase.LocationAsync(a, id, r, ct));
    [HttpPost("sites/{id}/lots")]
    public Task<IActionResult> Lot(string id, DistributedExistingLotRequest r, CancellationToken ct) => Run(a => useCase.ImportLotAsync(a, id, r, ct));
    [HttpPost("inbound-plans")]
    public Task<IActionResult> Inbound(DistributedInboundPlanRequest r, CancellationToken ct) => Run(a => useCase.InboundAsync(a, r, ct));
    [HttpPost("inbound-plans/{id}/receipts")]
    public Task<IActionResult> Receive(string id, DistributedInboundReceiptRequest r, CancellationToken ct) => Run(a => useCase.ReceiveAsync(a, id, r, ct));
    [HttpPost("inbound-plans/{id}/receipt-batches")]
    public Task<IActionResult> ReceiveBatch(string id, DistributedInboundReceiptBatchRequest r, CancellationToken ct) => Run(a => useCase.ReceiveBatchAsync(a, id, r, ct));
    [HttpPost("waves")]
    public Task<IActionResult> CreateWave(DistributedWaveCreateRequest r, CancellationToken ct) => Run(a => useCase.CreateWaveAsync(a, r, ct));
    [HttpPost("waves/{id}/orders")]
    public Task<IActionResult> Order(string id, DistributedOrderRequest r, CancellationToken ct) => Run(a => useCase.OrderAsync(a, id, r, ct));
    [HttpPost("waves/{id}/orders/{orderId}/ledger")]
    public Task<IActionResult> LinkOrderLedger(string id, string orderId, DistributedOrderLedgerLinkRequest r, CancellationToken ct)
        => Run(a => useCase.LinkOrderLedgerAsync(a, id, orderId, r, ct));
    [HttpPost("waves/{id}/close")]
    public Task<IActionResult> Close(string id, DistributedCommandRequest r, CancellationToken ct) => Run(a => useCase.CloseAsync(a, id, r, ct));
    [HttpPost("waves/{id}/plan")]
    public Task<IActionResult> Plan(string id, DistributedRoundPlanRequest r, CancellationToken ct) => Run(a => useCase.PlanAsync(a, id, r, ct));
    [HttpPost("waves/{id}/accept")]
    public Task<IActionResult> Accept(string id, DistributedRoundAcceptRequest r, CancellationToken ct) => Run(a => useCase.AcceptAsync(a, id, r, ct));
    [HttpPost("waves/{id}/picks")]
    public Task<IActionResult> Pick(string id, DistributedPickRequest r, CancellationToken ct) => Run(a => useCase.PickAsync(a, id, r, ct));
    [HttpPost("waves/{id}/pick-batches")]
    public Task<IActionResult> PickBatch(string id, DistributedPickBatchRequest r, CancellationToken ct) => Run(a => useCase.PickBatchAsync(a, id, r, ct));
    [HttpPost("waves/{id}/deliveries")]
    public Task<IActionResult> Deliver(string id, DistributedDeliveryRequest r, CancellationToken ct) => Run(a => useCase.DeliverAsync(a, id, r, ct));
    [HttpPost("waves/{id}/delivery-batches")]
    public Task<IActionResult> DeliverBatch(string id, DistributedDeliveryBatchRequest r, CancellationToken ct) => Run(a => useCase.DeliverBatchAsync(a, id, r, ct));
    [HttpPost("waves/{id}/cancel")]
    public Task<IActionResult> Cancel(string id, DistributedCancelRequest r, CancellationToken ct) => Run(a => useCase.CancelAsync(a, id, r, ct));
    [HttpPost("waves/{id}/returns")]
    public Task<IActionResult> Return(string id, DistributedReturnRequest r, CancellationToken ct) => Run(a => useCase.ReturnAsync(a, id, r, ct));
    [HttpPost("waves/{id}/return-batches")]
    public Task<IActionResult> ReturnBatch(string id, DistributedReturnBatchRequest r, CancellationToken ct) => Run(a => useCase.ReturnBatchAsync(a, id, r, ct));
    [HttpPost("waves/{id}/acknowledgements")]
    public Task<IActionResult> Acknowledge(string id, DistributedAcknowledgementRequest r, CancellationToken ct) => Run(a => useCase.AcknowledgeAsync(a, id, r, ct));
    private async Task<IActionResult> Run<T>(Func<DistributedActor, Task<T>> action)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        try { return Ok(await action(new(id, User.IsInRole(역할명.기사), User.IsInRole(역할명.서버관리자)))); }
        catch (DistributedFulfillmentException ex) { return StatusCode(ex.Status, new { errorCode = ex.Code, message = "권한·동의·판본·재고·운송 근거를 다시 확인해 주세요." }); }
    }
}
