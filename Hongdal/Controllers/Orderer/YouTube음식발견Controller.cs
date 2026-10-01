using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Content;
using Hongdal.Filters;
using Hongdal.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.Services.Versioning;

namespace Hongdal.Controllers.Orderer;

[ApiController]
[Authorize]
[HongdalApiVersion(
    HongdalProductVersion.V2_5,
    FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
[RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[Route("api/v1/orderer/youtube-food-discovery")]
public sealed class YouTube음식발견Controller : ControllerBase
{
    private readonly IYouTube음식상품발견Service _service;

    public YouTube음식발견Controller(IYouTube음식상품발견Service service)
    {
        _service = service;
    }

    [HttpGet("channels")]
    public async Task<IActionResult> 음식채널목록(
        [FromQuery] string? countryCode,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _service.음식채널목록조회Async(countryCode, take, cancellationToken));

    [HttpGet("countries")]
    public async Task<IActionResult> 음식채널국가집계(
        CancellationToken cancellationToken = default)
        => Ok(await _service.음식채널국가집계조회Async(cancellationToken));

    [HttpGet("products")]
    [AllowAnonymous]
    public async Task<IActionResult> 공개상품후보목록(
        [FromQuery] string? channelId,
        [FromQuery] string? countryCode,
        [FromQuery] string? candidateType,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _service.공개상품후보목록조회Async(
            channelId,
            countryCode,
            candidateType,
            take,
            cancellationToken);
        return Ok(candidates.Select(ToCommunityShareCandidate));
    }

    [HttpPost("products/{candidateId:long}/intents")]
    public async Task<IActionResult> 구매의향등록(
        [FromRoute] long candidateId,
        [FromBody] YouTube상품구매의향등록요청Dto 요청,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var result = await _service.구매의향등록Async(
            candidateId,
            요청,
            userId,
            User.Identity?.Name ?? userId,
            cancellationToken);
        if (!result.성공)
        {
            return this.ToProblemActionResult(result.메시지, result.상태코드);
        }

        return Ok(result.값);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");

    private static YouTube음식커뮤니티공유후보Dto ToCommunityShareCandidate(YouTube상품후보Dto candidate)
        => new(
            candidate.후보Id,
            candidate.상품명,
            candidate.브랜드명,
            candidate.원산지국가코드,
            candidate.후보유형,
            candidate.영상구간초,
            candidate.발견근거,
            candidate.VideoId,
            candidate.영상제목,
            candidate.영상게시일시Utc,
            candidate.영상썸네일Url,
            candidate.YouTube시청Url,
            candidate.ChannelId,
            candidate.채널명,
            candidate.채널국가코드);
}
