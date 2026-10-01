using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Services.Community;
using Hongdal.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Authoring,
    HongdalModuleKind.Api,
    "운영자 글의 문맥별 AI 이미지 계획·생성·첨부 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "이미지 생성은 외부 비용이 발생할 수 있고 결과는 사람이 검토한 뒤에만 게시글에 첨부합니다.")]
[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Api,
    "서버 관리자 글쓰기 이미지 요청을 검증하고 계획 또는 생성 UseCase로 전달",
    ContractType = typeof(ICommunityAuthoringImageService),
    FlowOrder = 30,
    Effects = HongdalCodeEffect.NetworkCall
              | HongdalCodeEffect.PersistentRead
              | HongdalCodeEffect.PersistentWrite
              | HongdalCodeEffect.ObjectStorageRead
              | HongdalCodeEffect.ObjectStorageWrite
              | HongdalCodeEffect.ThirdPartyApiCall
              | HongdalCodeEffect.MayIncurExternalCost,
    Boundary = "서버관리자 정책을 요구하며 완료된 AI 이미지만 게시글 첨부 경계로 전달합니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/admin/content/information/authoring/images")]
[Authorize(Policy = "서버관리자전용")]
public sealed class CommunityAuthoringImagesController : ControllerBase
{
    private readonly ICommunityAuthoringImagePromptPlanner _promptPlanner;
    private readonly ICommunityAuthoringImageService _imageService;
    private readonly I커뮤니티게시글첨부UseCase _communityPostAttachmentUseCase;

    public CommunityAuthoringImagesController(
        ICommunityAuthoringImagePromptPlanner promptPlanner,
        ICommunityAuthoringImageService imageService,
        I커뮤니티게시글첨부UseCase communityPostAttachmentUseCase)
    {
        _promptPlanner = promptPlanner;
        _imageService = imageService;
        _communityPostAttachmentUseCase = communityPostAttachmentUseCase;
    }

    [HttpPost("prompt-plan")]
    public ActionResult<CommunityAuthoringImagePromptPlanResponse> Plan(
        [FromBody] CommunityAuthoringImagePromptPlanRequest request)
    {
        try
        {
            return Ok(_promptPlanner.Plan(request));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(400, exception.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<CommunityAuthoringImageTaskResponse>> Generate(
        [FromBody] CommunityAuthoringImageGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _imageService.GenerateAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(400, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "이미지 생성 서비스를 사용할 수 없습니다",
                detail: exception.Message);
        }
    }

    [HttpGet("{jobCode}")]
    public async Task<ActionResult<CommunityAuthoringImageTaskResponse>> Get(
        string jobCode,
        [FromQuery] bool refreshProvider = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _imageService.GetAsync(jobCode, refreshProvider, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{jobCode}/post-attachments/{postId:long}")]
    public async Task<IActionResult> Attach(
        string jobCode,
        long postId,
        [FromBody] CommunityAuthoringGeneratedImageAttachRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await _imageService.OpenCompletedImageAsync(jobCode, cancellationToken);
            await using var stream = new MemoryStream(file.Content, writable: false);
            var result = await _communityPostAttachmentUseCase.첨부업로드Async(
                postId,
                new 커뮤니티게시글첨부업로드Command(
                    request.Password,
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Content.LongLength),
                cancellationToken);
            return this.ToActionResult(result);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(CreateProblem(404, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblem(409, exception.Message));
        }
    }

    private static ProblemDetails CreateProblem(int status, string detail)
        => new()
        {
            Status = status,
            Title = status switch
            {
                400 => "글쓰기 요청을 확인해 주세요",
                409 => "이미지 첨부 상태를 확인해 주세요",
                _ => "이미지 생성 작업을 찾을 수 없음"
            },
            Detail = detail
        };
}
