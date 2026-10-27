using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    /// <summary>기존 Solo Runtime의 같은 세션·직렬 명령 경계에서 선택적 출고 기능을 실행한다.</summary>
    public sealed partial class LocalSimulationRuntime : ISimulationFulfillmentRuntime
    {
        public ValueTask<SimulationFulfillmentStateSnapshot?> GetFulfillmentStateAsync(
            string sessionStableId, CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).GetFulfillmentState(), cancellationToken);

        public ValueTask<SimulationFulfillmentPlanSnapshot> PreviewFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentOrderRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).PreviewFulfillment(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentCommand request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).ConfirmFulfillment(request), cancellationToken);
    }
}
