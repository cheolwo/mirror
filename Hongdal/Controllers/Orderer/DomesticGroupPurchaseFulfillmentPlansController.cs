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
[Route("api/v1/orderer/domestic-group-purchases/{campaignId:guid}/fulfillment-plans")]
public sealed class DomesticGroupPurchaseFulfillmentPlansController : ControllerBase
{
    private readonly IDomesticGroupPurchaseFulfillmentPlanService service;

    public DomesticGroupPurchaseFulfillmentPlansController(
        IDomesticGroupPurchaseFulfillmentPlanService service)
    {
        this.service = service;
    }

    [HttpPost("preview")]
    public IActionResult Preview(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseFulfillmentPlanRequest request)
    {
        request.GroupPurchaseCampaignId = campaignId;
        return Ok(service.Preview(request));
    }

    [HttpPost("order-drafts")]
    public async Task<IActionResult> CreateOrderDraft(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            request.GroupPurchaseCampaignId = campaignId;
            var draft = await service.CreateOrderDraftAsync(CurrentUserId(), request, cancellationToken);
            return CreatedAtAction(
                nameof(GetOrderDraft),
                new { campaignId, draftId = draft.DraftId },
                draft);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("order-drafts/{draftId:guid}")]
    public async Task<IActionResult> GetOrderDraft(
        Guid campaignId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var draft = await service.GetOrderDraftAsync(CurrentUserId(), draftId, cancellationToken);
        return draft is not null && draft.Plan.GroupPurchaseCampaignId == campaignId
            ? Ok(draft)
            : NotFound();
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
