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
[Route("api/v1/orderer/domestic-group-purchases/{campaignId:guid}/vehicle-recommendations")]
public sealed class DomesticGroupPurchaseVehicleRecommendationsController : ControllerBase
{
    private readonly IDomesticGroupPurchaseVehicleRecommendationService _service;

    public DomesticGroupPurchaseVehicleRecommendationsController(
        IDomesticGroupPurchaseVehicleRecommendationService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseVehicleRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            request.GroupPurchaseCampaignId = campaignId;
            return Ok(await _service.PreviewAsync(request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
