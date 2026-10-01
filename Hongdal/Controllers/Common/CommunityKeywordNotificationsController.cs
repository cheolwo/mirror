using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/community/keyword-notifications")]
public sealed class CommunityKeywordNotificationsController : ControllerBase
{
    private readonly ICommunityKeywordInboxService _service;

    public CommunityKeywordNotificationsController(ICommunityKeywordInboxService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? appKey,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.ListAsync(
                CurrentUserId(),
                appKey,
                unreadOnly,
                page,
                pageSize,
                cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        [FromQuery] string? appKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await _service.GetUnreadCountAsync(CurrentUserId(), appKey, cancellationToken);
            return Ok(new { count });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
        => await _service.MarkReadAsync(CurrentUserId(), id, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(
        [FromQuery] string? appKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedCount = await _service.MarkAllReadAsync(CurrentUserId(), appKey, cancellationToken);
            return Ok(new { updatedCount });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
