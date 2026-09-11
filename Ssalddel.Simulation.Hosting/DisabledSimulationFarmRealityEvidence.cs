using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting;

public sealed class DisabledSimulationFarmRealityOperationalReader
    : ISimulationFarmRealityOperationalReader
{
    public Task<SimulationFarmRealityEvidenceBundle> ReadApprovedAsync(
        string areaSetStableId, string canonicalProductStableId,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("SimulationFarmRealityEvidenceDisabled");
}

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E6,
    "세계 의미·인과·근거와 플레이 준비도 책임을 제공한다.",
    Boundary = "운영 근거와 Simulation 규칙 및 E 승격을 분리한다.")]
public sealed class DisabledSimulationFarmRealityEvidenceStore
    : ISimulationFarmRealityEvidenceStore
{
    public Task<SimulationFarmRealityEvidenceSyncResponse> UpsertAsync(
        SimulationFarmRealityEvidenceBundle bundle,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("SimulationFarmRealityEvidenceDisabled");

    public Task<SimulationFarmRealityEvidenceBundle> ReadLatestAsync(
        string areaSetStableId, string canonicalProductStableId,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("SimulationFarmRealityEvidenceNotFound");
}
