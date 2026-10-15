using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Contracts.Admin.Settlement;

namespace Ssalddel.Controllers.Admin.Settlement;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/driver-payout-batches")]
[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
public sealed class 배송료정산배치Controller(I배송료정산배치UseCase useCase) : ControllerBase
{
    [HttpPost("prepare")]
    [SsalddelApiOperation(SsalddelOperation.Execute)]
    public async Task<IActionResult> 준비([FromBody] 배송료정산배치준비요청 request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.준비Async(request, cancellationToken));

    [HttpGet("{batchId:long}")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 조회(long batchId, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.조회Async(batchId, cancellationToken));

    [HttpPost("{batchId:long}/revalidate")]
    [SsalddelApiOperation(SsalddelOperation.Execute)]
    public async Task<IActionResult> 재검증(long batchId, [FromBody] 배송료정산배치재검증요청 request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.재검증Async(batchId, request, cancellationToken));
}
