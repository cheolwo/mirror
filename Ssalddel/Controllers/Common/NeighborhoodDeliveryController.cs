using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Controllers;
using Ssalddel.Services.Community;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[Route(NeighborhoodDeliveryRoutes.Api)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiCapability(SsalddelCapability.TransportRequest)]
[SsalddelApiAudience(SsalddelActor.CommunityMember)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.CoreLogistics)]
public sealed class 생활배송의뢰Controller(I생활배송의뢰UseCase useCase, I생활배송기사정보제공동의Service disclosure,
    I생활배송지도Service map, I생활배송배차선택Service choices, I생활배송처리처Service? fulfillment = null,
    I자동수행Coordinator? automatic = null, ICurrentUserAccessor? currentUser = null,
    I생활배송엑셀초안Service? excelInput = null) : CommunityControllerBase
{
    [HttpGet("input-template")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public IActionResult 입력양식()
    {
        Response.Headers.CacheControl = "private, no-store";
        if (excelInput is null) return StatusCode(503);
        try { return Ok(excelInput.양식조회()); }
        catch (InvalidOperationException) { return StatusCode(503); }
    }

    [HttpPost("input-preview")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 입력미리보기([FromBody] NeighborhoodDeliveryInputPreviewRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return excelInput is null ? StatusCode(503) : this.ToActionResult(await excelInput.미리보기Async(request, cancellationToken));
    }

    [HttpGet("{requestId}/auto-fulfillment")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 자동수행상태(string requestId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.내상세Async(requestId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.") : Ok(item);
    }

    [HttpPost("{requestId}/auto-fulfillment-consent")]
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    public async Task<IActionResult> 자동수행동의(string requestId, [FromBody] AutoFulfillmentConsent request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (automatic is null) return StatusCode(503);
        if (await useCase.내상세Async(requestId, cancellationToken) is null) return this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.");
        if (string.IsNullOrWhiteSpace(currentUser?.UserId)) return Unauthorized();
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.NeighborhoodDelivery, requestId);
        var result = await automatic.사전동의저장Async(key, currentUser.UserId, request, cancellationToken);
        if (result.IsFailed) return this.ToActionResult(result);
        if (!request.Revoked)
        {
            await automatic.예약Async(key, request.ClientRequestId, cancellationToken);
            result = await automatic.선정실행Async(key, request.ClientRequestId, cancellationToken);
            if (result.IsFailed) return this.ToActionResult(result);
        }
        return Ok(await useCase.내상세Async(requestId, cancellationToken));
    }

    [HttpPost("{requestId}/auto-fulfillment-refresh")]
    [SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> 자동수행새로고침(string requestId, [FromBody] AutoFulfillmentActionRequest request, CancellationToken cancellationToken)
        => 자동수행명령Async(requestId, request, false, cancellationToken);

    [HttpPost("{requestId}/auto-fulfillment-cancel")]
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    public Task<IActionResult> 자동수행취소(string requestId, [FromBody] AutoFulfillmentActionRequest request, CancellationToken cancellationToken)
        => 자동수행명령Async(requestId, request, true, cancellationToken);

    private async Task<IActionResult> 자동수행명령Async(string requestId, AutoFulfillmentActionRequest request, bool cancel, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (automatic is null) return StatusCode(503);
        if (await useCase.내상세Async(requestId, ct) is null) return this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.");
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.NeighborhoodDelivery, requestId);
        var result = cancel ? await automatic.취소Async(key, request, ct) : await automatic.재확인Async(key, request, ct);
        return result.IsFailed ? this.ToActionResult(result) : Ok(await useCase.내상세Async(requestId, ct));
    }

    [HttpGet("{requestId}/fulfillment-options")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 처리처견적(string requestId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return fulfillment is null ? StatusCode(503) : this.ToActionResult(await fulfillment.내견적Async(requestId, cancellationToken));
    }

    [HttpPost("{requestId}/fulfillment-choice")]
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    public async Task<IActionResult> 처리처선택(string requestId, [FromBody] NeighborhoodFulfillmentChoiceRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (fulfillment is null) return StatusCode(503);
        var result = await fulfillment.선택Async(requestId, request, cancellationToken);
        return result.IsFailed ? this.ToActionResult(result) : Ok(await useCase.내상세Async(requestId, cancellationToken));
    }

    [HttpPost("{requestId}/fulfillment-refresh")]
    [SsalddelApiOperation(SsalddelOperation.Record)]
    public async Task<IActionResult> 처리처새로고침(string requestId, [FromBody] NeighborhoodFulfillmentActionRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (fulfillment is null) return StatusCode(503);
        var result = await fulfillment.새로고침Async(requestId, request, cancellationToken);
        return result.IsFailed ? this.ToActionResult(result) : Ok(await useCase.내상세Async(requestId, cancellationToken));
    }

    [HttpPost("{requestId}/fulfillment-cancel")]
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    public async Task<IActionResult> 처리처취소(string requestId, [FromBody] NeighborhoodFulfillmentActionRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (fulfillment is null) return StatusCode(503);
        var result = await fulfillment.취소Async(requestId, request, cancellationToken);
        return result.IsFailed ? this.ToActionResult(result) : Ok(await useCase.내상세Async(requestId, cancellationToken));
    }
    [HttpPost("quote")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 견적([FromBody] NeighborhoodDeliveryRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await useCase.견적Async(request, cancellationToken));
    }

    [HttpPost]
    [SsalddelApiOperation(SsalddelOperation.Request)]
    public async Task<IActionResult> 등록([FromBody] NeighborhoodDeliveryRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await useCase.등록Async(request, cancellationToken);
        if (result.IsFailed) return this.ToActionResult(result);
        return result.Value.IdempotentReplay ? Ok(result.Value)
            : CreatedAtAction(nameof(내상세), new { requestId = result.Value.RequestId }, result.Value);
    }

    [HttpGet("mine")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 내목록([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await useCase.내목록Async(page, cancellationToken));
    }

    [HttpPost("{requestId}/dispatch-choice")]
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    public async Task<IActionResult> 배차선택(string requestId, [FromBody] NeighborhoodDispatchChoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await choices.선택Async(requestId, request, cancellationToken);
        if (result.IsFailed) return this.ToActionResult(FluentResults.Result.Fail<NeighborhoodDeliveryResponse>(result.Errors));
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await useCase.내상세Async(requestId, cancellationToken));
    }

    [HttpPost("{requestId}/driver-disclosure")]
    [SsalddelApiOperation(SsalddelOperation.Request)]
    public async Task<IActionResult> 기사정보제공동의(string requestId, [FromBody] NeighborhoodDeliveryDisclosureRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await disclosure.기록Async(requestId, request, cancellationToken));

    [HttpGet("{requestId}/map")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 내지도(string requestId, [FromQuery] bool includeRoute = false, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await map.내지도Async(requestId, includeRoute, cancellationToken);
        return item is null ? this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.") : Ok(item);
    }

    [HttpGet("{requestId}")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 내상세(string requestId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.내상세Async(requestId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.") : Ok(item);
    }
}
