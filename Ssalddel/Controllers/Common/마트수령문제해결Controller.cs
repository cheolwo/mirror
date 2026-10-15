using System.Security.Claims;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Mart;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[Route("api/v1/mart/orders/{orderId:long}/receipt-resolution")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V3_5, FeatureKey = VersionFeatureFlagKeys.SsalddelMartWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.SsalddelMart)]
public sealed class 마트수령문제해결Controller(I마트수령확인UseCase useCase) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> 조회(long orderId, CancellationToken ct) => Run(() => useCase.해결조회Async(orderId, ct));
    [HttpPost("proposals")]
    public Task<IActionResult> 제안(long orderId, [FromBody] 마트수령해결제안요청 request, CancellationToken ct)
        => Run(() => useCase.해결제안Async(orderId, request, ct), true);
    [HttpPost("consents")]
    public Task<IActionResult> 동의(long orderId, [FromBody] 마트수령해결동의요청 request, CancellationToken ct)
        => Run(() => useCase.해결동의Async(orderId, request, ct), true);
    [HttpGet("requests/{requestId:guid}")]
    public Task<IActionResult> 요청결과(long orderId, Guid requestId, CancellationToken ct)
        => Run(() => useCase.해결요청결과조회Async(orderId, requestId, ct));
    private async Task<IActionResult> Run(Func<Task<Result<마트수령문제해결Dto>>> execute, bool command = false)
    {
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"))) return Unauthorized();
        var result = await execute(); var response = this.ToActionResult(result);
        if (command && result.IsFailed && response is ObjectResult { Value: ProblemDetails problem, StatusCode: 400 or 404 or 409 or 422 })
            problem.Extensions["commandRejected"] = true;
        return response;
    }
}
