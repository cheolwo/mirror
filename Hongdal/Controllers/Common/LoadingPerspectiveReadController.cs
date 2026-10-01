using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.WarehouseFulfillment)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Warehouse)]
[ApiController]
[Authorize]
[Route("api/v1/loading-perspectives")]
public sealed class LoadingPerspectiveReadController(
    ILoadingPerspectiveReadService service) : ControllerBase
{
    [HttpGet("orderer")]
    public Task<IActionResult> 주문자상차목록([FromQuery] 상차관점목록조회요청 request, CancellationToken cancellationToken)
        => Query(상차업무관점코드.주문자, null, request, cancellationToken);

    [HttpGet("seller")]
    public Task<IActionResult> 판매자상차목록([FromQuery] 상차관점목록조회요청 request, CancellationToken cancellationToken)
        => Query(상차업무관점코드.판매자, null, request, cancellationToken);

    [HttpGet("warehouse")]
    public Task<IActionResult> 창고관리자상차목록([FromQuery] 상차관점목록조회요청 request, CancellationToken cancellationToken)
        => Query(상차업무관점코드.창고관리자, null, request, cancellationToken);

    [HttpGet("transport")]
    public Task<IActionResult> 운송담당자상차목록([FromQuery] 상차관점목록조회요청 request, CancellationToken cancellationToken)
        => Query(상차업무관점코드.운송담당자, null, request, cancellationToken);

    [HttpGet("community-ledgers/{communityLedgerId}")]
    public Task<IActionResult> 공동원장상차목록(
        string communityLedgerId,
        [FromQuery] 상차관점목록조회요청 request,
        CancellationToken cancellationToken)
        => Query(상차업무관점코드.공동원장, communityLedgerId, request, cancellationToken);

    private async Task<IActionResult> Query(
        string perspectiveCode,
        string? communityLedgerId,
        상차관점목록조회요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await service.QueryAsync(
            perspectiveCode,
            communityLedgerId,
            request,
            cancellationToken));
}
