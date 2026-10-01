using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.TraditionalMarkets;
using Hongdal.Services.TraditionalMarkets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/traditional-markets")]
public sealed class TraditionalMarketsController : ControllerBase
{
    private readonly ITraditionalMarketCatalogService _catalogService;

    public TraditionalMarketsController(ITraditionalMarketCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<TraditionalMarketListResponse>> Search(
        [FromQuery] TraditionalMarketSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _catalogService.SearchAsync(request, cancellationToken));

    [HttpGet("{marketCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<TraditionalMarketResponse>> Get(
        string marketCode,
        CancellationToken cancellationToken)
    {
        var market = await _catalogService.GetAsync(marketCode, cancellationToken);
        return market is null ? NotFound() : Ok(market);
    }

    [HttpPost("sync")]
    [Authorize(Policy = "서버관리자전용")]
    public async Task<ActionResult<TraditionalMarketSyncResponse>> Sync(
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.SyncAsync(cancellationToken);
        return result.Status == TraditionalMarketSyncStatuses.Completed
            ? Ok(result)
            : Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "전통시장 공공데이터 동기화에 실패했습니다.",
                detail: result.ErrorMessage);
    }
}
