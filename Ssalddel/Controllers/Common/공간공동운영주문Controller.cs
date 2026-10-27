using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[Route(공간공동운영주문Routes.Api)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelApiAudience(SsalddelActor.Orderer)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Decide)]
public sealed class 공간공동운영주문Controller(I공간공동운영주문UseCase useCase) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> 목록(string ledgerId, CancellationToken ct) => Read(async () => await useCase.목록Async(ledgerId, ct));
    [HttpGet("candidates")]
    public Task<IActionResult> 후보(string ledgerId, [FromQuery] string cycleId, CancellationToken ct)
        => Read(async () => await useCase.후보Async(ledgerId, cycleId, ct));
    [HttpGet("{orderRequestId:guid}")]
    public Task<IActionResult> 상세(string ledgerId, Guid orderRequestId, CancellationToken ct)
        => Read(async () => await useCase.조회Async(ledgerId, orderRequestId, ct));
    [HttpGet("requests/{requestId:guid}")]
    public Task<IActionResult> 결과(string ledgerId, Guid requestId, CancellationToken ct)
        => Read(async () => await useCase.요청결과Async(ledgerId, requestId, ct));
    [HttpPost("{orderRequestId:guid}/commands")]
    public async Task<IActionResult> 변경(string ledgerId, Guid orderRequestId, [FromBody] 공간공동운영주문명령Request request, CancellationToken ct)
    {
        if (!Authenticated()) return Unauthorized();
        var result = await useCase.변경Async(ledgerId, orderRequestId, request, ct);
        return this.ToActionResult(result);
    }
    private async Task<IActionResult> Read(Func<Task<object?>> query)
    {
        if (!Authenticated()) return Unauthorized();
        try { var item = await query(); return item is null ? NotFound() : Ok(item); }
        catch (SpaceOrderException e) { return StatusCode(e.Status, new { code = e.Code, message = "현재 권한과 주문 조건을 다시 확인해 주세요." }); }
    }
    private bool Authenticated() => User.Identity?.IsAuthenticated == true
        && !string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"));
}
