using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/node-sticker-store")]
public sealed class 노드스티커상점Controller : ControllerBase
{
    private readonly I노드스티커상점UseCase _useCase;

    public 노드스티커상점Controller(I노드스티커상점UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet("items")]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _useCase.상품목록Async(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("items/{itemKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        [FromRoute] string itemKey,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.상품상세Async(itemKey, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("entitlements/me")]
    [Authorize]
    public async Task<IActionResult> GetMyEntitlements(CancellationToken cancellationToken)
    {
        var result = await _useCase.내보유권목록Async(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("fake-pg/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmFakePg(
        [FromBody] 노드스티커FakePg결제승인Request request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.페이크결제승인Async(request, cancellationToken);
        return this.ToActionResult(result);
    }
}
