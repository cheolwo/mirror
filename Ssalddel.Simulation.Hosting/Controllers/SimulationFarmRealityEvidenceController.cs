using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/reality-evidence/farm-potato")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationFarmRealityEvidence,
    SsalddelCodeLayer.Api,
    "감자 Farm 현실근거의 명시적 동기화와 읽기 전용 조회 경계를 제공한다.",
    StepKey = "api.farm-reality-evidence",
    DependsOnStepKeys = new[] { "contract.farm-reality-evidence" },
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.SharedPublicData | SsalddelCodeDataScope.DerivedWorld,
    WritesTo = SsalddelCodeDataScope.DerivedWorld,
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 20,
    Boundary = "동기화는 명시적 요청에서만 수행하며 Tick·Unity 조회 중 Provider를 호출하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E6,
    "세계 의미·인과·근거와 플레이 준비도 책임을 제공한다.",
    Boundary = "운영 근거와 Simulation 규칙 및 E 승격을 분리한다.")]
public sealed class SimulationFarmRealityEvidenceController(
    SimulationFarmRealityEvidenceService service) : ControllerBase
{
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SimulationFarmRealityEvidenceSyncResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationFarmRealityEvidenceSyncResponse>> Sync(
        [FromBody] SimulationFarmRealityEvidenceSyncRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.SyncAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(SimulationFarmRealityEvidenceBundle),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationFarmRealityEvidenceBundle>> Get(
        [FromQuery] string areaSetStableId =
            SimulationFarmRealityEvidenceCodes.FarmAreaSetStableId,
        [FromQuery] string canonicalProductStableId =
            SimulationFarmRealityEvidenceCodes.PotatoProductStableId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.ReadAsync(areaSetStableId,
                canonicalProductStableId, cancellationToken));
        }
        catch (InvalidOperationException error)
            when (error.Message == "SimulationFarmRealityEvidenceNotFound")
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.Message });
        }
    }
}
