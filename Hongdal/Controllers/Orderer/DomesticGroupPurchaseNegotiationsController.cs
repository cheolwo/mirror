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
[Route("api/v1/orderer/domestic-group-purchases/{campaignId:guid}/negotiation")]
public sealed class DomesticGroupPurchaseNegotiationsController : ControllerBase
{
    private readonly IDomesticGroupPurchaseNegotiationService service;

    public DomesticGroupPurchaseNegotiationsController(IDomesticGroupPurchaseNegotiationService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetTimeline(Guid campaignId, CancellationToken cancellationToken)
        => Ok(await service.GetTimelineAsync(campaignId, cancellationToken));

    [HttpPost("events")]
    public Task<IActionResult> AppendEvent(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseNegotiationEventRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => service.AppendEventAsync(campaignId, CurrentUserId(), request, cancellationToken));

    [HttpPost("issues")]
    public Task<IActionResult> OpenIssue(
        Guid campaignId,
        [FromBody] DomesticGroupPurchaseNegotiationIssueRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => service.OpenIssueAsync(campaignId, CurrentUserId(), request, cancellationToken));

    [HttpPost("issues/{issueId:guid}/positions")]
    public Task<IActionResult> AddPosition(
        Guid campaignId,
        Guid issueId,
        [FromBody] DomesticGroupPurchaseDeliberationPositionRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => service.AddPositionAsync(campaignId, issueId, CurrentUserId(), request, cancellationToken));

    [HttpPost("issues/{issueId:guid}/resolution")]
    public Task<IActionResult> ResolveIssue(
        Guid campaignId,
        Guid issueId,
        [FromBody] DomesticGroupPurchaseNegotiationResolutionRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ResolveIssueAsync(campaignId, issueId, CurrentUserId(), request, cancellationToken));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
