using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.WorldProjection;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Driver.Progress05;

[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.TransportExecution)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.WorldRolePerspective,
    SsalddelCodeLayer.Api,
    "인증된 기사에게 배정 하차 업무의 주거공동체 World 관점을 제공한다.",
    Effects = SsalddelCodeEffect.PersistentRead,
    ContractType = typeof(ResidentialPickupPerspectiveResponse),
    FlowOrder = 30,
    Boundary = "현재 운송 관계가 확인된 하차 object만 제공하고 수령자 주소, 연락처, 사용자 식별자와 주문번호는 반환하지 않는다.")]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route(ResidentialPickupPerspectiveRoutes.Transporter)]
public sealed class 기사주거공동체World관점Controller(
    IResidentialPickupPerspectiveUseCase perspectiveUseCase) : DriverControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 운송자관점조회(CancellationToken cancellationToken)
        => this.ToActionResult(await perspectiveUseCase.QueryTransporterAsync(cancellationToken));
}
