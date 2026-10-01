using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Orderer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Orderer;

[ApiController]
[Authorize]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/orderer/domestic-group-purchases/{campaignId:guid}/producer-connections")]
public sealed class DomesticGroupPurchaseProducerConnectionsController : ControllerBase
{
    private readonly IDomesticGroupPurchaseProducerConnectionService service;

    public DomesticGroupPurchaseProducerConnectionsController(
        IDomesticGroupPurchaseProducerConnectionService service)
    {
        this.service = service;
    }

    [HttpGet("candidates")]
    public async Task<IActionResult> SearchCandidates(
        Guid campaignId,
        [FromQuery] string? search,
        [FromQuery] string? regionCode,
        [FromQuery] string? product,
        CancellationToken cancellationToken)
    {
        if (campaignId == Guid.Empty)
        {
            return BadRequest(new { message = "공동구매 캠페인 식별자가 필요합니다." });
        }

        return Ok(await service.SearchCandidatesAsync(search, regionCode, product, cancellationToken));
    }

    [HttpPost("contact-request-drafts")]
    public async Task<IActionResult> CreateDraft(
        Guid campaignId,
        [FromBody] DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            request.GroupPurchaseCampaignId = campaignId;
            var draft = await service.CreateDraftAsync(CurrentUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetDraft), new { campaignId, draftId = draft.DraftId }, draft);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("contact-request-drafts/{draftId:guid}")]
    public async Task<IActionResult> GetDraft(
        Guid campaignId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var draft = await service.GetDraftAsync(CurrentUserId(), draftId, cancellationToken);
        return draft is not null && draft.GroupPurchaseCampaignId == campaignId
            ? Ok(draft)
            : NotFound();
    }

    [HttpGet("representatives")]
    public async Task<IActionResult> SearchRepresentatives(
        Guid campaignId,
        [FromQuery] string? search,
        [FromQuery] string? operatingAreaCode,
        [FromQuery] string? product,
        CancellationToken cancellationToken)
    {
        if (campaignId == Guid.Empty)
        {
            return BadRequest(new { message = "공동구매 캠페인 식별자가 필요합니다." });
        }

        return Ok(await service.SearchRepresentativesAsync(
            search,
            operatingAreaCode,
            product,
            cancellationToken));
    }

    [HttpPost("supply-offer-drafts")]
    public async Task<IActionResult> CreateSupplyOfferDraft(
        Guid campaignId,
        [FromBody] DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            request.GroupPurchaseCampaignId = campaignId;
            var draft = await service.CreateSupplyOfferDraftAsync(CurrentUserId(), request, cancellationToken);
            return CreatedAtAction(
                nameof(GetSupplyOfferDraft),
                new { campaignId, draftId = draft.DraftId },
                draft);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("supply-offer-drafts/{draftId:guid}")]
    public async Task<IActionResult> GetSupplyOfferDraft(
        Guid campaignId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var draft = await service.GetSupplyOfferDraftAsync(CurrentUserId(), draftId, cancellationToken);
        return draft is not null && draft.GroupPurchaseCampaignId == campaignId
            ? Ok(draft)
            : NotFound();
    }

    [HttpPost("compatibility-previews")]
    public IActionResult PreviewCompatibility(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request)
    {
        if (campaignId == Guid.Empty)
        {
            return BadRequest(new { message = "공동구매 캠페인 식별자가 필요합니다." });
        }

        return Ok(service.PreviewCompatibility(request));
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
