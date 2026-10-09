using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiAudience(SsalddelActor.Orderer)]
[SsalddelApiAudience(SsalddelActor.Seller)]
[SsalddelApiAudience(SsalddelActor.WarehouseManager)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Decide)]
[Route("api/v1/neighborhood-settlements")]
[SsalddelApiContractName("NeighborhoodSettlementController")]
public sealed class NeighborhoodSettlementController(INeighborhoodSettlementUseCase useCase) : ControllerBase
{
    [HttpGet] public Task<IActionResult> Mine(CancellationToken ct) => Run(a => useCase.MySummaryAsync(a, ct));
    [HttpGet("{id}")] public Task<IActionResult> Get(string id, CancellationToken ct) => Run(a => useCase.GetAsync(id, a, ct));
    [HttpGet("receipts/{requestId:guid}")] public Task<IActionResult> Receipt(Guid requestId, CancellationToken ct) => Run(a => useCase.ReceiptAsync(a, requestId, ct));
    [HttpPost] public Task<IActionResult> Create(NeighborhoodSettlementAgreementRequest r, CancellationToken ct) => Run(a => useCase.CreateAsync(a, r, ct));
    [HttpPost("{id}/terms")] public Task<IActionResult> Terms(string id, NeighborhoodSettlementAgreementRequest r, CancellationToken ct) => Run(a => useCase.TermsAsync(id, a, r, ct));
    [HttpPost("{id}/consents")] public Task<IActionResult> Consent(string id, NeighborhoodSettlementConsentRequest r, CancellationToken ct) => Run(a => useCase.ConsentAsync(id, a, r, ct));
    [HttpPost("{id}/accruals")] public Task<IActionResult> Accrue(string id, NeighborhoodSettlementAccrualRequest r, CancellationToken ct) => Run(a => useCase.AccrueAsync(id, a, r, ct));
    [HttpPost("{id}/entry-confirmations")] public Task<IActionResult> Confirm(string id, NeighborhoodSettlementEntryConfirmRequest r, CancellationToken ct) => Run(a => useCase.ConfirmEntryAsync(id, a, r, ct));
    [HttpPost("{id}/disputes")] public Task<IActionResult> Dispute(string id, NeighborhoodSettlementDisputeRequest r, CancellationToken ct) => Run(a => useCase.DisputeAsync(id, a, r, ct));
    [HttpPost("{id}/adjustments")] public Task<IActionResult> Adjust(string id, NeighborhoodSettlementAdjustmentRequest r, CancellationToken ct) => Run(a => useCase.AdjustmentAsync(id, a, r, ct));
    [HttpPost("{id}/payment-reports")] public Task<IActionResult> Report(string id, NeighborhoodSettlementPaymentReportRequest r, CancellationToken ct) => Run(a => useCase.ReportPaymentAsync(id, a, r, ct));
    [HttpPost("{id}/payment-receipts")] public Task<IActionResult> Receive(string id, NeighborhoodSettlementPaymentReceiptRequest r, CancellationToken ct) => Run(a => useCase.ConfirmReceiptAsync(id, a, r, ct));

    private async Task<IActionResult> Run<T>(Func<string, Task<T>> operation)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized();
        try { return Ok(await operation(actor)); }
        catch (NeighborhoodSettlementException ex)
        { return StatusCode(ex.StatusCode, new { errorCode = ex.Code, message = "당사자·약정 판본·완료 근거·지급 확인 상태를 다시 확인해 주세요." }); }
    }
}
