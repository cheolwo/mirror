using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Api,
    "게시글 추천과 일반·첨부 댓글 참여 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "자발적 추천·댓글과 비밀번호 기반 본인 삭제만 연결하며 운영자 심의를 수행하지 않습니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/posts")]
public sealed class 커뮤니티게시글참여Controller : ControllerBase
{
    private readonly I커뮤니티게시글참여UseCase _participationUseCase;

    public 커뮤니티게시글참여Controller(I커뮤니티게시글참여UseCase participationUseCase)
    {
        _participationUseCase = participationUseCase;
    }

    [HttpPost("{id:long}/recommendations")]
    [AllowAnonymous]
    public async Task<IActionResult> Recommend(
        long id,
        [FromBody] PlatformCommunityPostRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var fallbackKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return this.ToActionResult(await _participationUseCase.추천Async(
            id,
            request,
            fallbackKey,
            cancellationToken));
    }

    [HttpGet("{id:long}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> ListComments(long id, CancellationToken cancellationToken)
        => this.ToActionResult(await _participationUseCase.댓글목록Async(id, cancellationToken));

    [HttpPost("{id:long}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateComment(
        long id,
        [FromBody] PlatformCommunityPostCommentCreateRequest request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _participationUseCase.댓글작성Async(
            id,
            request,
            cancellationToken));

    [HttpDelete("{id:long}/comments/{commentId:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteComment(
        long id,
        long commentId,
        [FromBody] PlatformCommunityPostPasswordRequest request,
        CancellationToken cancellationToken)
        => this.ToNoContentActionResult(await _participationUseCase.댓글삭제Async(
            id,
            commentId,
            request,
            cancellationToken));

    [HttpGet("attachments/{attachmentId:long}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> ListAttachmentComments(
        long attachmentId,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _participationUseCase.첨부댓글목록Async(
            attachmentId,
            cancellationToken));

    [HttpPost("attachments/{attachmentId:long}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateAttachmentComment(
        long attachmentId,
        [FromBody] PlatformCommunityPostAttachmentCommentCreateRequest request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _participationUseCase.첨부댓글작성Async(
            attachmentId,
            request,
            cancellationToken));

    [HttpDelete("attachments/{attachmentId:long}/comments/{commentId:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteAttachmentComment(
        long attachmentId,
        long commentId,
        [FromBody] PlatformCommunityPostPasswordRequest request,
        CancellationToken cancellationToken)
        => this.ToNoContentActionResult(await _participationUseCase.첨부댓글삭제Async(
            attachmentId,
            commentId,
            request,
            cancellationToken));
}
