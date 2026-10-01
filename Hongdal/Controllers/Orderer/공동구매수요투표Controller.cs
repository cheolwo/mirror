using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Filters;
using Hongdal.Services.Community;
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
[Route("api/v1/orderer/group-purchase-demand-votes")]
public sealed class 공동구매수요투표Controller : ControllerBase
{
    private readonly I커뮤니티투표UseCase _useCase;
    private readonly I공동구매원장절차Service _ledgerWorkflow;

    public 공동구매수요투표Controller(
        I커뮤니티투표UseCase useCase,
        I공동구매원장절차Service ledgerWorkflow)
    {
        _useCase = useCase;
        _ledgerWorkflow = ledgerWorkflow;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? communityScope,
        [FromQuery] string? hsCode,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.목록Async(
            "OrdererApp",
            communityScope,
            hsCode,
            cancellationToken);
        if (result.IsSuccess)
        {
            result.Value.Items = result.Value.Items
                .Where(x => x.VoteKind == CommunityVoteKindCodes.GroupPurchaseDemand)
                .ToArray();
        }

        return this.ToActionResult(result);
    }

    [HttpGet("{voteId:guid}")]
    public async Task<IActionResult> Get(Guid voteId, CancellationToken cancellationToken)
    {
        var result = await _useCase.상세Async(voteId, cancellationToken);
        if (result.IsSuccess && result.Value.VoteKind != CommunityVoteKindCodes.GroupPurchaseDemand)
        {
            return NotFound();
        }

        return this.ToActionResult(result);
    }

    [HttpGet("{voteId:guid}/ledger-progress")]
    public async Task<IActionResult> GetLedgerProgress(
        Guid voteId,
        CancellationToken cancellationToken)
    {
        var progress = await _ledgerWorkflow.조회Async(voteId, cancellationToken);
        return progress is null ? NotFound() : Ok(progress);
    }

    [HttpPost("{voteId:guid}/ledger-progress")]
    public async Task<IActionResult> AdvanceLedgerProgress(
        Guid voteId,
        [FromBody] CommunityGroupPurchaseLedgerProgressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var progress = await _ledgerWorkflow.진행Async(
                voteId,
                request,
                userId,
                cancellationToken);
            return progress is null ? NotFound() : Ok(progress);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CommunityVoteCreateRequest request,
        CancellationToken cancellationToken)
    {
        var scopeKey = User.FindFirstValue(주문자집단배송권ClaimTypes.ScopeKey);
        request.AppKey = "OrdererApp";
        request.VoteKind = CommunityVoteKindCodes.GroupPurchaseDemand;
        request.CreatedByDisplayName = string.IsNullOrWhiteSpace(request.CreatedByDisplayName)
            ? User.Identity?.Name ?? "공동구매 참여자"
            : request.CreatedByDisplayName;
        if (!string.IsNullOrWhiteSpace(scopeKey))
        {
            if (string.IsNullOrWhiteSpace(request.CommunityScope) || request.CommunityScope == "platform")
            {
                request.CommunityScope = scopeKey;
            }

            if (request.GroupPurchase is not null
                && string.IsNullOrWhiteSpace(request.GroupPurchase.ServiceAreaKey)
                && request.GroupPurchase.ParticipationPolicyCode is CommunityVoteParticipationPolicyCodes.ServiceAreaOnly
                    or CommunityVoteParticipationPolicyCodes.Hybrid)
            {
                request.GroupPurchase.ServiceAreaKey = scopeKey;
                request.GroupPurchase.ServiceAreaLabel = User.FindFirstValue(주문자집단배송권ClaimTypes.DisplayName)
                    ?? scopeKey;
            }
        }

        var result = await _useCase.생성Async(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { voteId = result.Value.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPost("{voteId:guid}/votes")]
    public async Task<IActionResult> CastVote(
        Guid voteId,
        [FromBody] CommunityVoteCastRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _useCase.상세Async(voteId, cancellationToken);
        if (!existing.IsSuccess)
        {
            return this.ToActionResult(existing);
        }

        if (existing.Value.VoteKind != CommunityVoteKindCodes.GroupPurchaseDemand)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var scopeKey = User.FindFirstValue(주문자집단배송권ClaimTypes.ScopeKey);
        request.VoterKey = userId;
        request.VoterDisplayName = string.IsNullOrWhiteSpace(request.VoterDisplayName)
            ? User.Identity?.Name ?? "공동구매 참여자"
            : request.VoterDisplayName;
        request.CommunityMembershipReference = scopeKey;
        request.ServiceAreaReference = scopeKey;

        var result = await _useCase.투표Async(voteId, request, cancellationToken);
        return this.ToActionResult(result);
    }
}
