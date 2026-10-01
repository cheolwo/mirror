using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
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
[Route("api/v1/orderer/group-purchase-demand-votes/{campaignId:guid}/group-import-ledger")]
public sealed class 공동수입원장Controller : ControllerBase
{
    private readonly I공동수입원장전환Service _service;

    public 공동수입원장Controller(I공동수입원장전환Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await _service.조회Async(campaignId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("preview")]
    public IActionResult Preview(
        Guid campaignId,
        [FromBody] CommunityGroupImportLedgerConversionRequest request)
    {
        request.GroupPurchaseCampaignId = campaignId;
        return Ok(_service.미리보기(request));
    }

    [HttpPost]
    public async Task<IActionResult> Convert(
        Guid campaignId,
        [FromBody] CommunityGroupImportLedgerConversionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            request.GroupPurchaseCampaignId = campaignId;
            var result = await _service.전환Async(
                request,
                CurrentUserId(),
                cancellationToken);
            return result.Created
                ? CreatedAtAction(nameof(Get), new { campaignId }, result)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
