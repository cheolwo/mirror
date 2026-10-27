using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    /// <summary>기존 Runtime과 공유하는 세션 접근자에 위임하며 별도의 세션이나 상태를 만들지 않는다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "같은 LocalProcess 세션의 다단계 출고 실행 경계를 제공한다.",
        Boundary = "기존 접근자와 Aggregate를 공유하며 별도 세션 저장소를 생성하지 않는다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter)]
    public sealed class LocalSimulationFulfillmentRuntime : ISimulationFulfillmentRuntime
    {
        private readonly 경영SimulationSessionAccessor sessions;

        public LocalSimulationFulfillmentRuntime(경영SimulationSessionAccessor sessions)
            => this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

        public ValueTask<SimulationFulfillmentStateSnapshot?> GetFulfillmentStateAsync(
            string sessionStableId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<SimulationFulfillmentStateSnapshot?>(
                sessions.Require(sessionStableId).GetFulfillmentState());
        }

        public ValueTask<SimulationFulfillmentPlanSnapshot> PreviewFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<SimulationFulfillmentPlanSnapshot>(
                sessions.Require(sessionStableId).PreviewFulfillment(request));
        }

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentCommand request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<경영SimulationSessionSnapshot>(
                sessions.Require(sessionStableId).ConfirmFulfillment(request));
        }
    }
}
