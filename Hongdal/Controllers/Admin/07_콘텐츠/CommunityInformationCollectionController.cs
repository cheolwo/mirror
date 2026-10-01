using System.Security.Claims;
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
    "운영자 자료 수집·SNS 조사·AI 초안 작업공간 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "서버관리자만 사용하며 외부 자료와 AI 초안은 검토 전 자동 게시하지 않습니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/admin/content/information")]
[Authorize(Policy = "서버관리자전용")]
public sealed class CommunityInformationCollectionController : ControllerBase
{
    private readonly ICommunityInformationCollectionService _service;
    private readonly IYouTubeSocialContextWorkspaceService _socialContextWorkspaceService;
    private readonly ICommunityAuthoringAiDraftService _aiDraftService;

    public CommunityInformationCollectionController(
        ICommunityInformationCollectionService service,
        IYouTubeSocialContextWorkspaceService socialContextWorkspaceService,
        ICommunityAuthoringAiDraftService aiDraftService)
    {
        _service = service;
        _socialContextWorkspaceService = socialContextWorkspaceService;
        _aiDraftService = aiDraftService;
    }

    [HttpGet("sources")]
    public ActionResult<IReadOnlyList<CommunityInformationSourceDto>> GetSources()
        => Ok(_service.GetSources());

    [HttpGet("candidates")]
    public async Task<ActionResult<CommunityInformationCollectionResponse>> GetCandidates(
        [FromQuery] CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken)
        => Ok(await _service.ReadAsync(query, cancellationToken));

    [HttpPost("authoring/ai-drafts")]
    public async Task<ActionResult<CommunityAuthoringAiDraftResponse>> GenerateAiDraft(
        [FromBody] CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _aiDraftService.GenerateAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(400, exception.Message));
        }
    }

    [HttpGet("social-media/sources")]
    public ActionResult<IReadOnlyList<SocialMediaResearchSourceDto>> GetSocialMediaSources()
        => Ok(_socialContextWorkspaceService.GetSources());

    [HttpPost("youtube-social-context/draft")]
    [HttpPost("youtube-social-context/workspaces/research")]
    public async Task<ActionResult<YouTubeSocialContextResearchResponse>> BuildYouTubeSocialContextDraft(
        [FromBody] YouTubeSocialContextResearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _socialContextWorkspaceService.ResearchAndSaveAsync(
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

    [HttpGet("youtube-social-context/workspaces")]
    public async Task<ActionResult<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>>> GetYouTubeSocialContextWorkspaces(
        [FromQuery] string? status,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _socialContextWorkspaceService.ListAsync(status, take, cancellationToken));

    [HttpGet("youtube-social-context/workspaces/by-video/{videoId}")]
    public async Task<ActionResult<YouTubeSocialContextWorkspaceDto>> GetYouTubeSocialContextWorkspaceByVideo(
        string videoId,
        CancellationToken cancellationToken)
    {
        var workspace = await _socialContextWorkspaceService.GetByVideoIdAsync(videoId, cancellationToken);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpGet("youtube-social-context/workspaces/{workspaceId}")]
    public async Task<ActionResult<YouTubeSocialContextWorkspaceDto>> GetYouTubeSocialContextWorkspace(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = await _socialContextWorkspaceService.GetAsync(workspaceId, cancellationToken);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpPut("youtube-social-context/workspaces/{workspaceId}/draft")]
    public async Task<ActionResult<YouTubeSocialContextWorkspaceDto>> UpdateYouTubeSocialContextWorkspaceDraft(
        string workspaceId,
        [FromBody] YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _socialContextWorkspaceService.UpdateDraftAsync(
                workspaceId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(CreateProblem(404, exception.Message));
        }
        catch (YouTubeSocialContextWorkspaceConcurrencyException exception)
        {
            return Conflict(CreateProblem(409, exception.Message));
        }
    }

    [HttpPost("youtube-social-context/workspaces/{workspaceId}/publication-links")]
    public async Task<ActionResult<YouTubeSocialContextWorkspaceDto>> LinkYouTubeSocialContextPublication(
        string workspaceId,
        [FromBody] YouTubeSocialContextPublicationLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _socialContextWorkspaceService.LinkPublicationAsync(
                workspaceId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(CreateProblem(404, exception.Message));
        }
        catch (YouTubeSocialContextWorkspaceConcurrencyException exception)
        {
            return Conflict(CreateProblem(409, exception.Message));
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name
           ?? "server-admin";

    private string CurrentDisplayName()
        => User.FindFirstValue("name")
           ?? User.Identity?.Name
           ?? "홍달 운영자";

    private static ProblemDetails CreateProblem(int status, string detail)
        => new()
        {
            Status = status,
            Title = status switch
            {
                400 => "글쓰기 요청을 확인해 주세요",
                409 => "작업공간 변경 충돌",
                _ => "작업공간을 찾을 수 없음"
            },
            Detail = detail
        };
}
