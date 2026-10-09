using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin;

/// <summary>격리 Simulation에서만 모의 업체의 개별 사건을 기록합니다. 실제 업체 webhook이 아닙니다.</summary>
[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/fulfillment-simulation")]
[SsalddelApiContractName("FulfillmentSimulationController")]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiCapability(SsalddelCapability.Dispatch)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.CoreLogistics)]
public sealed class 자동수행모의Controller(I자동수행Coordinator coordinator) : ControllerBase
{
    [HttpPost("{attemptId}/evidence")]
    [SsalddelApiContractName("RecordMockEvidence")]
    [SsalddelApiOperation(SsalddelOperation.Record)]
    public async Task<IActionResult> 모의사건기록(string attemptId, [FromBody] FulfillmentMockEvidenceRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!coordinator.Enabled)
        {
            var problem = new ProblemDetails { Title = "자동 수행은 활성화된 Simulation에서만 기록할 수 있습니다.", Status = 403 };
            problem.Extensions["errorCode"] = "FulfillmentRoutingDisabled";
            return StatusCode(403, problem);
        }
        return this.ToActionResult(await coordinator.모의증거기록Async(attemptId, request, ct));
    }
}
