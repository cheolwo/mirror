using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>같은 Simulation 세션의 출고 후보 조회·미리보기·확정 포트다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "다단계 출고의 LocalProcess·RemoteHost 공통 실행 경계를 제공한다.",
        Boundary = "Simulation 상태만 다루며 실제 재고 예약·직원 배정·운영 출고를 실행하지 않는다.")]
    public interface ISimulationFulfillmentRuntime
    {
        ValueTask<SimulationFulfillmentStateSnapshot?> GetFulfillmentStateAsync(
            string sessionStableId, CancellationToken cancellationToken = default);

        ValueTask<SimulationFulfillmentPlanSnapshot> PreviewFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentOrderRequest request,
            CancellationToken cancellationToken = default);

        ValueTask<경영SimulationSessionSnapshot> ConfirmFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentCommand request,
            CancellationToken cancellationToken = default);
    }
}
