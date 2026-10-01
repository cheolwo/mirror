using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/community/posts/{postId:long}/opportunities")]
public sealed class CommunityPostOpportunitiesController : ControllerBase
{
    private readonly ICommunityPostOpportunityQueryUseCase _queryUseCase;
    private readonly ICommunityPostParticipationUseCase _participationUseCase;
    private readonly ICommunityPostProfessionalParticipationService _professionalParticipationService;
    private readonly ICommunityPostMeatImportReadinessUseCase _meatImportReadinessUseCase;

    public CommunityPostOpportunitiesController(
        ICommunityPostOpportunityQueryUseCase queryUseCase,
        ICommunityPostParticipationUseCase participationUseCase,
        ICommunityPostProfessionalParticipationService professionalParticipationService,
        ICommunityPostMeatImportReadinessUseCase meatImportReadinessUseCase)
    {
        _queryUseCase = queryUseCase;
        _participationUseCase = participationUseCase;
        _professionalParticipationService = professionalParticipationService;
        _meatImportReadinessUseCase = meatImportReadinessUseCase;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CommunityPostOpportunityListResponse>> Get(
        long postId,
        [FromQuery] string? displayLanguage,
        CancellationToken cancellationToken)
    {
        var result = await _queryUseCase.GetAsync(postId, displayLanguage, cancellationToken);
        return result is null
            ? NotFoundProblem("커뮤니티 게시글을 찾을 수 없습니다.")
            : Ok(result);
    }

    [HttpPost("context-discovery")]
    [AllowAnonymous]
    public async Task<ActionResult<CommunityPostContextDiscoveryResponse>> GetContextDiscovery(
        long postId,
        [FromBody] CommunityPostContextDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _queryUseCase.GetContextDiscoveryAsync(postId, request, cancellationToken);
            return result is null
                ? NotFoundProblem("커뮤니티 게시글을 찾을 수 없습니다.")
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "주변 정보 조회 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    [HttpPost("meat-import-readiness/start")]
    [Authorize]
    public async Task<ActionResult<StartCommunityMeatImportReadinessResponse>> StartMeatImportReadiness(
        long postId,
        [FromBody] StartCommunityMeatImportReadinessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _meatImportReadinessUseCase.StartAsync(
                postId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "이 게시글에서 정보 협업을 시작할 권한이 없습니다.",
                detail: ex.Message);
        }
        catch (CommunityPostOpportunityConflictException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "게시글 원장 연결이 충돌했습니다.",
                detail: ex.Message);
        }
        catch (MeatImportReadinessConcurrencyException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "육류 수입 준비 정보가 이미 변경되었습니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "정보 협업 시작 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    [HttpPost("participation/start")]
    [Authorize]
    public async Task<ActionResult<StartCommunityPostParticipationResponse>> StartParticipation(
        long postId,
        [FromBody] StartCommunityPostParticipationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _participationUseCase.StartParticipationAsync(
                postId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "참여 관심 모집을 시작하려면 로그인이 필요합니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "참여 관심 모집 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    [HttpPost("participation/provisional-ledger")]
    [Authorize]
    public async Task<ActionResult<PromoteCommunityPostParticipationResponse>> PromoteParticipation(
        long postId,
        [FromBody] PromoteCommunityPostParticipationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _participationUseCase.PromoteParticipationAsync(
                postId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "가원장을 만들 권한이 없습니다.",
                detail: ex.Message);
        }
        catch (CommunityPostOpportunityConflictException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "게시글 원장 연결이 충돌했습니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "가원장 생성 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    [HttpPost("participation/professionals")]
    [Authorize]
    public async Task<ActionResult<JoinCommunityPostProfessionalResponse>> JoinProfessional(
        long postId,
        [FromBody] JoinCommunityPostProfessionalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _professionalParticipationService.JoinAsync(
                postId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "전문 역할 참여 자격을 확인할 수 없습니다.",
                detail: ex.Message);
        }
        catch (CommunityPostOpportunityConflictException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "게시글과 가원장 연결이 충돌했습니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "전문가 참여 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    [HttpPost("participation/party-roles")]
    [Authorize]
    public async Task<ActionResult<JoinCommunityPostPartyRoleResponse>> JoinPartyRole(
        long postId,
        [FromBody] JoinCommunityPostPartyRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _professionalParticipationService.JoinPartyRoleAsync(
                postId,
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "거래 당사자 역할을 수락하려면 로그인이 필요합니다.",
                detail: ex.Message);
        }
        catch (CommunityPostOpportunityConflictException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "게시글과 가원장 연결이 충돌했습니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "거래 당사자 역할 참여 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? string.Empty;

    private string CurrentDisplayName()
        => User.Identity?.Name
           ?? User.FindFirstValue("name")
           ?? "참여자";

    private ObjectResult NotFoundProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "커뮤니티 게시글을 찾을 수 없습니다.",
            detail: detail);
}
