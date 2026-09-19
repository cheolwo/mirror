using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Finance;
using Ssalddel.Contracts.Admin.Finance;

namespace Ssalddel.Controllers.Admin.Operations;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/operations/finance")]
[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.PlatformOperations)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiContractName("OperationalFinanceController")]
public sealed class 운영재무Controller(I운영재무조회UseCase useCase) : ControllerBase
{
    [HttpGet("metadata")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public IActionResult 메타데이터조회() => Ok(useCase.메타데이터조회());

    [HttpGet("events")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 사건목록조회(
        [FromQuery] string? profileStableId,
        [FromQuery] string? currencyCode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.사건목록조회Async(
            profileStableId,
            currencyCode,
            from,
            to,
            page,
            pageSize,
            cancellationToken));

    [HttpGet("management-balances")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 관리계정잔액조회(
        [FromQuery] string? currencyCode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.관리계정잔액조회Async(currencyCode, from, to, cancellationToken));

    [HttpGet("cash-flow-summary")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 현금흐름요약조회(
        [FromQuery] string currencyCode,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.현금흐름요약조회Async(currencyCode, from, to, cancellationToken));

    [HttpGet("reconciliation-exceptions")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 대사예외조회(
        [FromQuery] string? statusCode,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.대사예외조회Async(statusCode, cancellationToken));
}
