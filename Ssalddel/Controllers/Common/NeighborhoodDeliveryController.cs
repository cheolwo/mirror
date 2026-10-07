using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
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
    I생활배송지도Service map, I생활배송배차선택Service choices) : CommunityControllerBase
{
    [HttpPost("quote")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 견적([FromBody] NeighborhoodDeliveryRequest request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.견적Async(request, cancellationToken));

    [HttpPost]
    [SsalddelApiOperation(SsalddelOperation.Request)]
    public async Task<IActionResult> 등록([FromBody] NeighborhoodDeliveryRequest request, CancellationToken cancellationToken)
    {
        var result = await useCase.등록Async(request, cancellationToken);
        if (result.IsFailed) return this.ToActionResult(result);
        return result.Value.IdempotentReplay ? Ok(result.Value)
            : CreatedAtAction(nameof(내상세), new { requestId = result.Value.RequestId }, result.Value);
    }

    [HttpGet("mine")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 내목록([FromQuery] int page = 1, CancellationToken cancellationToken = default)
        => Ok(await useCase.내목록Async(page, cancellationToken));

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
        var item = await useCase.내상세Async(requestId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("본인 배송 의뢰를 찾을 수 없습니다.") : Ok(item);
    }
}
