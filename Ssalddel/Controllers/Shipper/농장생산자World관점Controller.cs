using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.WorldProjection;

namespace Ssalddel.Controllers.Shipper;

[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiAudience(SsalddelActor.ShipperOrSeller)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiCapability(SsalddelCapability.SalesFulfillment)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.WorldRolePerspective,
    SsalddelCodeLayer.Api,
    "인증 생산자에게 자신이 소유한 농장·재배·센서의 World 관점을 제공한다.",
    Effects = SsalddelCodeEffect.PersistentRead,
    ContractType = typeof(FarmProducerPerspectiveResponse),
    FlowOrder = 30,
    Boundary = "소유권은 서버가 현재 사용자로 필터링하며 위치, 연락처, 사용자 식별자는 반환하지 않는다.")]
[ApiController]
[Authorize]
[Route(FarmProducerPerspectiveRoutes.Producer)]
public sealed class 농장생산자World관점Controller(
    IFarmProducerPerspectiveUseCase perspectiveUseCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 생산자관점조회(CancellationToken cancellationToken)
        => this.ToActionResult(await perspectiveUseCase.QueryAsync(cancellationToken));
}
