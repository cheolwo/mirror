using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Education;
using Hongdal.Services.Education;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/education")]
public sealed class 교육과정참여Controller : ControllerBase
{
    private readonly I교육과정참여Service _service;

    public 교육과정참여Controller(I교육과정참여Service service)
    {
        _service = service;
    }

    [HttpPost("applications")]
    public async Task<IActionResult> 신청(
        [FromBody] 교육과정신청요청 요청,
        CancellationToken cancellationToken)
    {
        var created = await _service.신청Async(요청, CurrentUserId(), cancellationToken);
        return CreatedAtAction(nameof(내신청목록조회), created);
    }

    [HttpGet("applications/mine")]
    public async Task<IActionResult> 내신청목록조회(CancellationToken cancellationToken)
        => Ok(await _service.내신청목록조회Async(CurrentUserId(), cancellationToken));

    [HttpDelete("applications/{신청Id:long}/personal-data")]
    public async Task<IActionResult> 내개인정보삭제(long 신청Id, CancellationToken cancellationToken)
    {
        await _service.개인정보삭제Async(신청Id, CurrentUserId(), false, cancellationToken);
        return NoContent();
    }

    [HttpGet("enrollments/{등록Id:long}/progress")]
    public async Task<IActionResult> 진행현황조회(long 등록Id, CancellationToken cancellationToken)
        => Ok(await _service.진행현황조회Async(등록Id, CurrentUserId(), false, cancellationToken));

    [HttpPost("enrollments/{등록Id:long}/submissions")]
    public async Task<IActionResult> 과제제출(
        long 등록Id,
        [FromBody] 교육과정과제제출요청 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.과제제출Async(등록Id, 요청, CurrentUserId(), cancellationToken));

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
