using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/community/keyword-subscriptions")]
public sealed class CommunityKeywordSubscriptionsController : ControllerBase
{
    private readonly ICommunityKeywordSubscriptionService _service;

    public CommunityKeywordSubscriptionsController(ICommunityKeywordSubscriptionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? appKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.ListAsync(CurrentUserId(), appKey, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(
        [FromBody] CommunityKeywordSubscriptionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.SubscribeAsync(CurrentUserId(), request, cancellationToken));
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

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Unsubscribe(long id, CancellationToken cancellationToken)
        => await _service.UnsubscribeAsync(CurrentUserId(), id, cancellationToken)
            ? NoContent()
            : NotFound();

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
