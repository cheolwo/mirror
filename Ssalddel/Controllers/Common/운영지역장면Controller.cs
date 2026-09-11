using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(
    SsalddelProductVersion.V2_5,
    FeatureKey = VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[Route(OperationalWorldSceneRoutes.AreaSnapshot)]
public sealed class 운영지역장면Controller(I운영지역장면조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperationalWorldSceneResponse>> 조회(
        string areaStableId,
        [FromQuery] long cursor = 0,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.조회Async(areaStableId, cursor, cancellationToken));
}
