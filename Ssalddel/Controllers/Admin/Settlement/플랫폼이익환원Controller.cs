using Ssalddel.Contracts.Common.PlatformProfit;
using Ssalddel.Application.Settlement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Finance;

namespace Ssalddel.Controllers.Admin.Settlement;

[SsalddelApiVersion(SsalddelProductVersion.V2_5)]
[ApiController]
[Authorize(Roles = "서버관리자")]
[Route("api/v1/admin/platform-profit-returns")]
[SsalddelApiContractName("PlatformProfitReturnsController")]
public sealed class 플랫폼이익환원Controller : ControllerBase
{
    private readonly I플랫폼수익환급UseCase _플랫폼이익환원UseCase;

    public 플랫폼이익환원Controller(I플랫폼수익환급UseCase 플랫폼이익환원UseCase)
    {
        _플랫폼이익환원UseCase = 플랫폼이익환원UseCase;
    }

    [HttpPost("revenues")]
    [SsalddelApiContractName("RecordRevenue")]
    [Ssalddel재무영향Profile(재무영향ProfileIds.수기수익Simulation, ConditionCode = "ManualScenarioInput")]
    public async Task<IActionResult> 수익기록(
        [FromBody] PlatformRevenueEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _플랫폼이익환원UseCase.수익기록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("policies")]
    [SsalddelApiContractName("CreatePolicy")]
    public async Task<IActionResult> 정책생성(
        [FromBody] PlatformProfitReturnPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _플랫폼이익환원UseCase.정책생성Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("schedules")]
    [SsalddelApiContractName("CreateSchedules")]
    public async Task<IActionResult> 일정생성(
        [FromBody] PlatformProfitReturnScheduleCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _플랫폼이익환원UseCase.스케줄생성Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("schedules")]
    [SsalddelApiContractName("ListSchedules")]
    public async Task<IActionResult> 일정목록조회(
        [FromQuery] string? participantUserId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var result = await _플랫폼이익환원UseCase.스케줄목록Async(participantUserId, from, to, cancellationToken);
        return this.ToActionResult(result);
    }
}
