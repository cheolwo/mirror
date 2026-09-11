using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.BusinessWorkflow
{
    public static class BusinessWorkflowRuntimeModeCodes
    {
        public const string LocalProcess = "LocalProcess";
        public const string RemoteHost = "RemoteHost";
    }

    public static class BusinessWorkflowAuthorityScopeCodes
    {
        public const string SimulationSession = "SimulationSession";
    }

    public static class BusinessWorkflowExperienceRoleCodes
    {
        public const string AutonomousNpcWorld = "AutonomousNpcWorld";
    }

    public class BusinessWorkflowRuntimeDescriptor
    {
        public string RuntimeStableId { get; set; } = string.Empty;
        public string ModeCode { get; set; } = string.Empty;
        public bool RequiresNetwork { get; set; }
        public string AuthorityScopeCode { get; set; } = string.Empty;
        public string ExperienceRoleCode { get; set; } = string.Empty;
        public bool AllowsOperationalDriverActions { get; set; }
        public bool ObservationPresentationOnly { get; set; }
        public string ContractRevision { get; set; } = string.Empty;
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
    }

    public interface I주문업무Runtime
    {
        ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달PreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달ConfirmRequest request,
            CancellationToken token = default);
        ValueTask<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령PreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령ConfirmRequest request,
            CancellationToken token = default);
    }

    public interface I음식점업무Runtime
    {
        ValueTask<경영SimulationSessionSnapshot> ConfirmRestaurantResponseAsync(
            string sessionStableId, Simulation음식점응답Request request,
            CancellationToken token = default);
    }

    public interface I배차업무Runtime
    {
        ValueTask<SimulationFreightDispatchPreviewSnapshot> PreviewFreightDispatchAsync(
            string sessionStableId, SimulationFreightDispatchPreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFreightDispatchAsync(
            string sessionStableId, SimulationFreightDispatchConfirmRequest request,
            CancellationToken token = default);
    }

    public interface I배송업무Runtime
    {
        ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달PreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달ConfirmRequest request,
            CancellationToken token = default);
    }

    public interface I창고업무Runtime
    {
        ValueTask<SimulationLogisticsMovementPreviewSnapshot> PreviewLogisticsMovementAsync(
            string sessionStableId, SimulationLogisticsMovementPreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmLogisticsMovementAsync(
            string sessionStableId, SimulationLogisticsMovementConfirmRequest request,
            CancellationToken token = default);
    }

    /// <summary>
    /// LocalProcess 또는 원격 Simulation Host의 가상 세션 업무 흐름 facade다.
    /// 운영 기사 앱의 수락·위치·픽업·전달 API가 아니며, 하위 Runtime의 상태 권위와
    /// 수명을 소유하거나 복제하지 않는다.
    /// </summary>
    public interface IBusinessWorkflowRuntime
    {
        BusinessWorkflowRuntimeDescriptor Descriptor { get; }
        IBusinessWorkflowRuleEngine Rules { get; }
        I주문업무Runtime Orders { get; }
        I음식점업무Runtime Restaurants { get; }
        I배차업무Runtime Dispatch { get; }
        I배송업무Runtime Delivery { get; }
        I창고업무Runtime Warehouse { get; }
    }
}
