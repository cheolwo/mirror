using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Common.Finance;

namespace Ssalddel.Controllers.Admin.Settlement05;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/driver-payouts")]
[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
public sealed class 기사지급승인Controller(I기사지급승인UseCase useCase) : ControllerBase
{
    [HttpGet]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 목록조회(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? driverId,
        CancellationToken cancellationToken)
        => this.ToActionResult(
            await useCase.목록조회Async(year, month, driverId, cancellationToken));

    [HttpPost("approve")]
    [SsalddelApiOperation(SsalddelOperation.Execute)]
    [Ssalddel재무영향Profile(재무영향ProfileIds.기사지급승인)]
    public async Task<IActionResult> 승인(
        [FromBody] 기사지급승인요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.승인Async(request, cancellationToken));
}
