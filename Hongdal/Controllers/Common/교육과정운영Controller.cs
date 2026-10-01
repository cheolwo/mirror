using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Education;
using Hongdal.Services.Education;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.Data;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize(Roles = 역할명.교육과정멘토 + "," + 역할명.서버관리자)]
[Route("api/v1/education/operations")]
public sealed class 교육과정운영Controller : ControllerBase
{
    private readonly I교육과정참여Service _service;

    public 교육과정운영Controller(I교육과정참여Service service)
    {
        _service = service;
    }

    [HttpPut("applications/{신청Id:long}/review")]
    public async Task<IActionResult> 신청심사(
        long 신청Id,
        [FromBody] 교육과정신청심사요청 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.심사Async(신청Id, 요청, CurrentUserId(), cancellationToken));

    [HttpGet("enrollments/{등록Id:long}/progress")]
    public async Task<IActionResult> 진행현황조회(long 등록Id, CancellationToken cancellationToken)
        => Ok(await _service.진행현황조회Async(등록Id, CurrentUserId(), IsAdministrator(), cancellationToken));

    [HttpPut("enrollments/{등록Id:long}/attendances")]
    public async Task<IActionResult> 참석기록(
        long 등록Id,
        [FromBody] 교육과정참석기록요청 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.참석기록Async(등록Id, 요청, CurrentUserId(), IsAdministrator(), cancellationToken));

    [HttpPut("submissions/{제출Id:long}/review")]
    public async Task<IActionResult> 과제확인(
        long 제출Id,
        [FromBody] 교육과정과제확인요청 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.과제확인Async(제출Id, 요청, CurrentUserId(), IsAdministrator(), cancellationToken));

    [HttpDelete("applications/{신청Id:long}/personal-data")]
    [Authorize(Roles = 역할명.서버관리자)]
    public async Task<IActionResult> 개인정보삭제(long 신청Id, CancellationToken cancellationToken)
    {
        await _service.개인정보삭제Async(신청Id, CurrentUserId(), true, cancellationToken);
        return NoContent();
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private bool IsAdministrator()
        => User.IsInRole(역할명.서버관리자);
}
