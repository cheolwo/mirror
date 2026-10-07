using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.WorldProjection;

namespace Ssalddel.Controllers.Orderer;

[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiAudience(SsalddelActor.Orderer)]
[SsalddelApiCapability(SsalddelCapability.OrderParticipation)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.WorldRolePerspective,
    SsalddelCodeLayer.Api,
    "인증된 주문자에게 본인 수령 업무의 주거공동체 World 관점을 제공한다.",
    Effects = SsalddelCodeEffect.PersistentRead,
    ContractType = typeof(ResidentialPickupPerspectiveResponse),
    FlowOrder = 30,
    Boundary = "본인 주문과 연결된 수령 object만 제공하고 주소, 연락처, 사용자 식별자와 주문번호는 반환하지 않는다.")]
[ApiController]
[Authorize]
[Route(ResidentialPickupPerspectiveRoutes.Orderer)]
public sealed class 주거공동체World관점Controller(
    IResidentialPickupPerspectiveUseCase perspectiveUseCase) : OrdererControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 주문자관점조회(CancellationToken cancellationToken)
        => this.ToActionResult(await perspectiveUseCase.QueryOrdererAsync(cancellationToken));
}
