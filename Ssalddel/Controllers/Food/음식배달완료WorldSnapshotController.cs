using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Food;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Food;

[SsalddelApiVersion(SsalddelProductVersion.V3_0, FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow, WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Authorize]
[Route("api/v1/food-delivery/world/areas/{areaStableId}/completed-lifecycles")]
public sealed class 음식배달완료WorldSnapshotController(
    I음식배달완료WorldSnapshot조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<음식배달완료WorldSnapshot목록응답>> 지역목록(
        string areaStableId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => Ok(await useCase.지역목록Async(areaStableId, take, cancellationToken));
}
