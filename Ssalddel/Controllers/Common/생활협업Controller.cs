using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[Route(NeighborhoodCollaborationRoutes.Api)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Api,
    "생활 협업 신청·조건 합의·상호 인계 확인과 최소 공개 기록 HTTP 경계",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "당사자 인증·서버 CAS가 권위이며 공개 댓글·금전 지급·전문 기사 권한을 확정하지 않습니다.")]
public sealed class 생활협업Controller(I생활협업UseCase useCase) : CommunityControllerBase
{
    [HttpPost]
    [SsalddelApiContractName("CreateNeighborhoodCollaboration")]
    public async Task<IActionResult> 신청([FromBody] NeighborhoodCollaborationCreateRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await useCase.신청Async(request, cancellationToken);
        if (result.IsFailed) return this.ToActionResult(result);
        return result.Value.IdempotentReplay ? Ok(result.Value)
            : CreatedAtAction(nameof(상세), new { stableId = result.Value.StableId }, result.Value);
    }

    [HttpGet("mine")]
    [SsalddelApiContractName("ListMyNeighborhoodCollaborations")]
    public async Task<IActionResult> 내목록([FromQuery] string scope = "all", [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await useCase.내목록Async(scope, page, cancellationToken));
    }

    [HttpGet("requests/{clientRequestId:guid}")]
    [SsalddelApiContractName("GetNeighborhoodCollaborationRequestResult")]
    public async Task<IActionResult> 요청결과(Guid clientRequestId, [FromQuery] string? stableId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.요청결과Async(clientRequestId, stableId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("현재 계정의 접수 결과가 확인되지 않았습니다.") : Ok(item);
    }

    [HttpGet("{stableId}")]
    [SsalddelApiContractName("GetMyNeighborhoodCollaboration")]
    public async Task<IActionResult> 상세(string stableId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.상세Async(stableId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("본인의 협업을 찾을 수 없습니다.") : Ok(item);
    }

    [HttpPost("{stableId}/commands")]
    [SsalddelApiContractName("ChangeNeighborhoodCollaboration")]
    public async Task<IActionResult> 변경(string stableId, [FromBody] NeighborhoodCollaborationCommandRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await useCase.변경Async(stableId, request, cancellationToken));
    }

    [HttpGet("opportunities")]
    [AllowAnonymous]
    [SsalddelApiContractName("ListNeighborhoodCollaborationOpportunities")]
    public async Task<IActionResult> 참여기회([FromQuery] long sourcePostId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await useCase.참여기회Async(sourcePostId, cancellationToken));
    }

    [HttpGet("public-history")]
    [AllowAnonymous]
    [SsalddelApiContractName("ListConsentedNeighborhoodCollaborationHistory")]
    public async Task<IActionResult> 공개이력([FromQuery] long sourcePostId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await useCase.공개이력Async(sourcePostId, cancellationToken));
    }
}
