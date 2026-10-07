using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Services.Commerce;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiContractName("CommerceProtectionController")]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[Route("api/v1/common/commerce")]
public sealed class 통신판매보호Controller(통신판매안내Service notices, 판매자확인Service sellers, I거래신원확인Gateway gateway,
    Microsoft.Extensions.Options.IOptions<통신판매운영Options>? options = null,
    살뜰.Data.SsalddelContext? db = null, Ssalddel.Services.Community.I생활협업Store? collaborations = null) : CommunityControllerBase
{
    [AllowAnonymous, HttpGet("notices")]
    public ActionResult<통신판매안내Response> 안내()
    {
        var response = notices.조회();
        if (!gateway.IsConfigured) { response.MissingRequirements = [.. response.MissingRequirements, "운영용 신원 확인 연결"]; response.IsOperationalReady = false; }
        return Ok(response);
    }
    [HttpGet("me/seller")]
    public Task<IActionResult> 내판매자(CancellationToken cancellationToken) => Run(() => sellers.내상태Async(Actor(), cancellationToken));
    [HttpPut("me/seller")]
    public Task<IActionResult> 판매자등록(판매자등록Request request, CancellationToken cancellationToken)
        => Run(() => sellers.등록Async(Actor(), request, cancellationToken));
    [HttpPost("me/seller/verification")]
    public Task<IActionResult> 신원확인(판매자확인Request request, CancellationToken cancellationToken)
        => Run(() => sellers.확인Async(Actor(), request, cancellationToken));
    [AllowAnonymous, HttpGet("sellers/{sellerId}/public")]
    public async Task<IActionResult> 공개판매자(string sellerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sellerId) || sellerId.Length > 160) return NotFound();
        var response = await sellers.공개Async(sellerId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
    [HttpGet("me/eligibility")]
    public async Task<IActionResult> 거래자격(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Actor())) return Unauthorized();
        var identity = await sellers.내상태Async(Actor(), cancellationToken);
        var missing = notices.운영부족().ToList();
        if (!gateway.IsConfigured) missing.Add("운영용 신원 확인 연결");
        if (!identity.IsAdult) missing.Add("서버 성년 확인");
        return Ok(new 거래보호상태Response { StatusCode = missing.Count == 0 ? "Ready" : "Blocked", CanTransact = missing.Count == 0,
            NoticeVersion = 통신판매안내Service.Version, MissingRequirements = missing });
    }
    [AllowAnonymous, HttpGet("restaurants/{restaurantId:long}/seller/public")]
    public async Task<IActionResult> 음식점판매자(long restaurantId, CancellationToken cancellationToken)
    {
        if (options is null || !options.Value.RestaurantSellerUserIds.TryGetValue(restaurantId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var sellerId)) return NotFound();
        var response = await sellers.공개Async(sellerId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;
    [HttpGet("posts/{postId:long}/seller/public")]
    public async Task<IActionResult> 생활글판매자(long postId, CancellationToken cancellationToken)
    {
        if (db is null) return NotFound();
        var post = await db.PlatformCommunityPosts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted
            && x.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Published && !x.IsReportBoardPost
            && x.Category == PlatformCommunityPostCategories.General && x.WorkflowTag == NeighborhoodExchange.WorkflowTag
            && (x.RoleTag == NeighborhoodExchange.Offer || x.RoleTag == NeighborhoodExchange.Need), cancellationToken);
        if (post is null) return NotFound();
        var seller = post.RoleTag == NeighborhoodExchange.Offer ? post.AuthorUserId : Actor();
        var response = seller is null ? null : await sellers.공개Async(seller, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
    [HttpGet("collaborations/{stableId}/seller/public")]
    public async Task<IActionResult> 협업판매자(string stableId, CancellationToken cancellationToken)
    {
        if (collaborations is null || stableId.Length > 160) return NotFound();
        var record = await collaborations.조회Async(stableId, cancellationToken);
        if (record is null || (record.OwnerUserId != Actor() && record.RequesterUserId != Actor())) return NotFound();
        var seller = record.ProviderRoleCode == "requester" ? record.RequesterUserId : record.OwnerUserId;
        var response = await sellers.공개Async(seller, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
    private async Task<IActionResult> Run(Func<Task<판매자확인Response>> work)
    {
        try { return Ok(await work()); }
        catch (거래보호Exception ex) { return StatusCode(ex.Status, new ProblemDetails { Status = ex.Status, Title = ex.Message, Extensions = { ["code"] = ex.Code } }); }
    }
}
