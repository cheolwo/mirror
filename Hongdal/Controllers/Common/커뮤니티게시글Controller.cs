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
    "게시글 조회·발행·음성·번역·원장 문맥 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "공개 읽기와 게시글 작성자 명령만 연결하며 첨부·댓글 참여·운영 심의는 별도 Controller가 처리합니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/posts")]
public sealed class 커뮤니티게시글Controller : ControllerBase
{
    private readonly I커뮤니티게시글조회UseCase _readUseCase;
    private readonly I커뮤니티게시글발행UseCase _publishingUseCase;
    private readonly I커뮤니티게시글음성조회Service _audioService;
    private readonly ICommunityPostTranslationService _translationService;
    private readonly I게시글원장선택조회Service _ledgerSelectionService;
    private readonly I게시글원장표시ContextService _ledgerContextService;

    public 커뮤니티게시글Controller(
        I커뮤니티게시글조회UseCase readUseCase,
        I커뮤니티게시글발행UseCase publishingUseCase,
        I커뮤니티게시글음성조회Service audioService,
        ICommunityPostTranslationService translationService,
        I게시글원장선택조회Service ledgerSelectionService,
        I게시글원장표시ContextService ledgerContextService)
    {
        _readUseCase = readUseCase;
        _publishingUseCase = publishingUseCase;
        _audioService = audioService;
        _translationService = translationService;
        _ledgerSelectionService = ledgerSelectionService;
        _ledgerContextService = ledgerContextService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] string? appKey,
        [FromQuery] string? category,
        [FromQuery] string? boardKey,
        [FromQuery] string? workflowTag,
        [FromQuery] string? roleTag,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _readUseCase.목록Async(
            appKey,
            category,
            boardKey,
            workflowTag,
            roleTag,
            page,
            pageSize,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("board-summaries")]
    [AllowAnonymous]
    public async Task<IActionResult> ListBoardSummaries(
        [FromQuery] string? appKey,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _readUseCase.게시판요약목록Async(appKey, cancellationToken));

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        [FromBody] PlatformCommunityPostCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _publishingUseCase.생성Async(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet("my-ledgers")]
    [Authorize]
    public async Task<IActionResult> ListMyLedgers(
        [FromQuery] string? workflowTag,
        CancellationToken cancellationToken)
        => Ok(await _ledgerSelectionService.연결가능원장목록조회Async(
            CurrentUserId(),
            workflowTag,
            cancellationToken));

    [HttpGet("shared-ledgers")]
    [AllowAnonymous]
    public async Task<IActionResult> ListSharedLedgers(
        [FromQuery] string? workflowTag,
        CancellationToken cancellationToken)
        => Ok(await _ledgerSelectionService.공유원장목록조회Async(
            CurrentUserId(),
            workflowTag,
            cancellationToken));

    [HttpGet("ledgers/{ledgerId}/context")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLedgerContext(
        string ledgerId,
        CancellationToken cancellationToken)
    {
        var context = await _ledgerContextService.조회Async(
            ledgerId,
            CurrentUserId(),
            cancellationToken);
        return context is null ? NotFound() : Ok(context);
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
        => this.ToActionResult(await _readUseCase.상세Async(id, cancellationToken));

    [HttpPost("{id:long}/translations/{targetLanguageCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> Translate(
        long id,
        string targetLanguageCode,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _translationService.GetOrCreateAsync(
            id,
            targetLanguageCode,
            cancellationToken));

    [HttpGet("{id:long}/audio")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAudio(long id, CancellationToken cancellationToken)
    {
        var audio = await _audioService.조회Async(
            id,
            CurrentUserId(),
            HttpContext.TraceIdentifier,
            cancellationToken);
        return audio is null ? NotFound() : Ok(audio);
    }

    [HttpGet("{id:long}/audio/segments/{sequence:int}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadAudio(
        long id,
        int sequence,
        CancellationToken cancellationToken)
    {
        var audio = await _audioService.다운로드Async(
            id,
            sequence,
            CurrentUserId(),
            HttpContext.TraceIdentifier,
            cancellationToken);
        return audio is null
            ? NotFound()
            : File(audio.Content, audio.ContentType, audio.FileName, enableRangeProcessing: true);
    }

    [HttpPut("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] PlatformCommunityPostUpdateRequest request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _publishingUseCase.수정Async(id, request, cancellationToken));

    [HttpDelete("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Delete(
        long id,
        [FromBody] PlatformCommunityPostPasswordRequest request,
        CancellationToken cancellationToken)
        => this.ToNoContentActionResult(await _publishingUseCase.삭제Async(
            id,
            request,
            cancellationToken));

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub");
}
