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
[Authorize]
[Route("api/v1/education/field-experiences")]
public sealed class 현장체험활동Controller : ControllerBase
{
    private readonly I현장체험활동UseCase _useCase;

    public 현장체험활동Controller(I현장체험활동UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost]
    public async Task<IActionResult> 생성(
        [FromBody] 현장체험활동생성요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.생성Async(request, CurrentUserId(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(조회), new { ledgerId = result.Value.원장Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet("{ledgerId}")]
    public async Task<IActionResult> 조회(string ledgerId, CancellationToken cancellationToken)
    {
        var result = await _useCase.조회Async(
            ledgerId,
            CurrentUserId(),
            CurrentSchoolKey(),
            IsAdministrator(),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{ledgerId}/activity-records")]
    public async Task<IActionResult> 활동기록(
        string ledgerId,
        [FromBody] 현장체험활동기록요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.활동기록Async(ledgerId, request, CurrentUserId(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{ledgerId}/guardian-approval")]
    public async Task<IActionResult> 보호자승인(
        string ledgerId,
        [FromBody] 현장체험보호자승인요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.보호자승인Async(ledgerId, request, CurrentUserId(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{ledgerId}/activity-records/{activityRecordId}/field-verification")]
    [Authorize(Roles = 역할명.현장체험지도자)]
    public async Task<IActionResult> 현장지도자확인(
        string ledgerId,
        string activityRecordId,
        [FromBody] 현장체험지도자확인요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.현장지도자확인Async(
            ledgerId,
            activityRecordId,
            request,
            CurrentUserId(),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{ledgerId}/submissions")]
    public async Task<IActionResult> 학교제출(
        string ledgerId,
        [FromBody] 현장체험학교제출요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.학교제출Async(ledgerId, request, CurrentUserId(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{ledgerId}/school-decisions")]
    [Authorize(Roles = 역할명.선생님 + "," + 역할명.서버관리자)]
    public async Task<IActionResult> 학교결정(
        string ledgerId,
        [FromBody] 현장체험학교결정요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.학교결정Async(
            ledgerId,
            request,
            CurrentUserId(),
            CurrentSchoolKey(),
            IsAdministrator(),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string? CurrentSchoolKey()
        => User.IsInRole(역할명.선생님)
            ? User.FindFirstValue("school_id") ?? User.FindFirstValue("교육기관Key")
            : null;

    private bool IsAdministrator()
        => User.IsInRole(역할명.서버관리자);
}
