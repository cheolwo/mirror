using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hongdal.Controllers.Common;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Api,
    "공개 게시판 조회와 인증된 개설 신청·운영 검토 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "공개 조회와 관리자 검토 endpoint의 인증 경계를 분리합니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/boards")]
public sealed class 커뮤니티게시판Controller : ControllerBase
{
    private readonly I커뮤니티게시판UseCase _useCase;

    public 커뮤니티게시판Controller(I커뮤니티게시판UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] string? appKey,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.목록Async(
            appKey,
            PlatformCommunityBoardRequestStatuses.Approved,
            includeReviewDetails: false,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("requests")]
    [Authorize(Policy = "서버관리자전용")]
    public async Task<IActionResult> ListRequests(
        [FromQuery] string? appKey,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.목록Async(
            appKey,
            status ?? PlatformCommunityBoardRequestStatuses.Pending,
            includeReviewDetails: true,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(
        [FromBody] PlatformCommunityBoardCreateRequest request,
        CancellationToken cancellationToken)
    {
        var requesterUserId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            return Unauthorized();
        }

        var result = await _useCase.신청Async(
            request,
            requesterUserId,
            CurrentUserDisplayName(),
            cancellationToken);
        if (result.IsFailed)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(List), new { appKey = result.Value.AppKey }, result.Value);
    }

    [HttpPost("{id:long}/approve")]
    [Authorize(Policy = "서버관리자전용")]
    public async Task<IActionResult> Approve(
        long id,
        [FromBody] PlatformCommunityBoardReviewRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerUserId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            return Unauthorized();
        }

        var result = await _useCase.승인Async(id, request, reviewerUserId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:long}/reject")]
    [Authorize(Policy = "서버관리자전용")]
    public async Task<IActionResult> Reject(
        long id,
        [FromBody] PlatformCommunityBoardReviewRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerUserId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            return Unauthorized();
        }

        var result = await _useCase.반려Async(id, request, reviewerUserId, cancellationToken);
        return this.ToActionResult(result);
    }

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub");

    private string CurrentUserDisplayName()
        => User.FindFirstValue("name")
           ?? User.Identity?.Name
           ?? "회원";
}
