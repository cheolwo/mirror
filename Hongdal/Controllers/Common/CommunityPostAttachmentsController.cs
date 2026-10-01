using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Api,
    "게시글 이미지 첨부 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "첨부 업로드만 연결하며 게시글 발행·댓글·운영 상태는 변경하지 않습니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/posts")]
public sealed class 커뮤니티게시글첨부Controller : ControllerBase
{
    private readonly I커뮤니티게시글첨부UseCase _attachmentUseCase;

    public 커뮤니티게시글첨부Controller(I커뮤니티게시글첨부UseCase attachmentUseCase)
    {
        _attachmentUseCase = attachmentUseCase;
    }

    [HttpPost("{id:long}/attachments")]
    [AllowAnonymous]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadAttachment(
        long id,
        [FromForm] PlatformCommunityPostAttachmentUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return this.ToProblemActionResult("업로드할 이미지 파일을 선택해야 합니다.");
        }

        await using var stream = request.File.OpenReadStream();
        var command = new 커뮤니티게시글첨부업로드Command(
            request.Password,
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length);
        return this.ToActionResult(await _attachmentUseCase.첨부업로드Async(
            id,
            command,
            cancellationToken));
    }
}

public sealed class PlatformCommunityPostAttachmentUploadRequest
{
    public string Password { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}
