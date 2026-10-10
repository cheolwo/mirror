using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Mart;
using Ssalddel.Controllers;
using Ssalddel.Filters;
using Ssalddel.Services.Operations;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Driver.Food;

[ApiController]
[Authorize(Roles = 역할명.기사)]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[Route("api/v1/driver/mart-deliveries")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelApiCapability(SsalddelCapability.FoodDelivery)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Execute)]
[SsalddelApiContractName("MartDriverWorkController")]
public sealed class 마트기사업무Controller(I마트기사업무Service service) : DriverControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : this.ToActionResult(await service.조회Async(CurrentUserId, ct));

    [HttpGet("{offerId}")]
    public async Task<IActionResult> 업무조회(string offerId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : this.ToActionResult(await service.업무조회Async(CurrentUserId, offerId, ct));

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> 요청결과조회(Guid requestId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : CommandResult(await service.요청결과조회Async(requestId, ct), markNotFound: false);

    [HttpPost("{offerId}/accept")]
    public async Task<IActionResult> 수락(string offerId, [FromBody] 마트기사수락요청 request, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : CommandResult(await service.수락Async(CurrentUserId, offerId, request, ct));

    [HttpPost("{offerId}/reject")]
    public async Task<IActionResult> 거절(string offerId, [FromBody] 마트기사거절요청 request, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : CommandResult(await service.거절Async(CurrentUserId, offerId, request, ct));

    [HttpPost("{offerId}/pickup")]
    public async Task<IActionResult> 픽업(string offerId, [FromBody] 마트기사픽업요청 request, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : CommandResult(await service.픽업Async(CurrentUserId, offerId, request, ct));

    [HttpPost("{offerId}/deliver")]
    public async Task<IActionResult> 전달(string offerId, [FromBody] 마트기사전달요청 request, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : CommandResult(await service.전달Async(CurrentUserId, offerId, request, ct));

    // 서비스가 확정 거절한 응답과 전송 결과를 알 수 없는 실패를 구분한다.
    // 인증 미들웨어·proxy 응답·5xx에는 이 표시를 붙이지 않는다.
    private IActionResult CommandResult<T>(Result<T> result, bool markNotFound = true)
    {
        var response = this.ToActionResult(result);
        if (result.IsFailed && response is ObjectResult { Value: ProblemDetails problem, StatusCode: 400 or 404 or 409 or 422 or 429 } failure
            && (markNotFound || failure.StatusCode != 404))
        {
            if (!result.Errors.Any(error => error.Metadata.TryGetValue("ErrorCode", out var code)
                    && code is string value && !string.IsNullOrWhiteSpace(value)))
                problem.Extensions["errorCode"] = "MartDriverCommandRejected";
            problem.Extensions["commandRejected"] = true;
            var currentRevision = result.Errors.Select(error => error.Metadata.TryGetValue("CurrentRevision", out var value) ? value : null)
                .OfType<long>().Cast<long?>().FirstOrDefault();
            if (currentRevision is >= 0) problem.Extensions["currentRevision"] = currentRevision.Value;
        }
        return response;
    }
}
