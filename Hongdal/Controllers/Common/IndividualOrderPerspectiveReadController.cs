using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/order-perspectives/individual-orders")]
public sealed class IndividualOrderPerspectiveReadController(
    IIndividualOrderPerspectiveReadService service) : ControllerBase
{
    [HttpGet("orderer")]
    public Task<IActionResult> 주문자목록(
        [FromQuery] 개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(개별주문관점코드.주문자, null, request, cancellationToken);

    [HttpGet("seller")]
    public Task<IActionResult> 판매자목록(
        [FromQuery] 개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(개별주문관점코드.판매자, null, request, cancellationToken);

    [HttpGet("warehouse")]
    public Task<IActionResult> 창고관리자목록(
        [FromQuery] 개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(개별주문관점코드.창고관리자, null, request, cancellationToken);

    [HttpGet("transport")]
    public Task<IActionResult> 운송담당자목록(
        [FromQuery] 개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(개별주문관점코드.운송담당자, null, request, cancellationToken);

    [HttpGet("community-ledgers/{communityLedgerId}")]
    public Task<IActionResult> 공동원장목록(
        string communityLedgerId,
        [FromQuery] 개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(개별주문관점코드.공동원장, communityLedgerId, request, cancellationToken);

    private async Task<IActionResult> Query(
        string perspective,
        string? communityLedgerId,
        개별주문관점목록조회요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await service.QueryAsync(
            perspective,
            communityLedgerId,
            request,
            cancellationToken));
}
