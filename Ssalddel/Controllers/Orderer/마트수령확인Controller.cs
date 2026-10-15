using System.Security.Claims;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Mart;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Orderer;

/// <summary>실제 마트 주문자의 수령·문제 진술만 다룹니다. 기사 전달, 비구속 구매 의향과 지급 명령은 별도입니다.</summary>
[ApiController]
[Authorize]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[Route("api/v1/orderer/mart/orders/{orderId:long}/receipt")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V3_5, FeatureKey = VersionFeatureFlagKeys.SsalddelMartWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.SsalddelMart)]
public sealed class 마트수령확인Controller(I마트수령확인UseCase useCase) : OrdererControllerBase
{
    [HttpGet]
    public Task<IActionResult> 조회(long orderId, CancellationToken ct)
        => Run(() => useCase.조회Async(orderId, ct));

    [HttpPost]
    public Task<IActionResult> 확인(long orderId, [FromBody] 마트수령확인요청 request, CancellationToken ct)
        => Run(() => useCase.확인Async(orderId, request, ct), command: true);

    [HttpPost("issues")]
    public Task<IActionResult> 문제신고(long orderId, [FromBody] 마트수령문제신고요청 request, CancellationToken ct)
        => Run(() => useCase.문제신고Async(orderId, request, ct), command: true);

    [HttpGet("requests/{requestId:guid}")]
    public Task<IActionResult> 요청결과조회(long orderId, Guid requestId, CancellationToken ct)
        => Run(() => useCase.요청결과조회Async(orderId, requestId, ct));

    private async Task<IActionResult> Run(Func<Task<Result<마트수령확인Dto>>> execute, bool command = false)
    {
        if (User.Identity?.IsAuthenticated != true
            || string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")))
            return Unauthorized();
        // 실행 주체는 UseCase가 같은 인증 accessor에서 읽어 실제 SQL 주문자와 대조합니다.
        var result = await execute();
        var response = this.ToActionResult(result);
        if (command && result.IsFailed
            && response is ObjectResult { Value: ProblemDetails problem, StatusCode: 400 or 404 or 409 or 422 } )
            problem.Extensions["commandRejected"] = true;
        // 요청 결과의 404는 접수 여부 미확정입니다. 확정 거절 표시를 붙이지 않습니다.
        return response;
    }
}
