using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.BusinessWorkflow
{
    public class BusinessWorkflowRuntime : IBusinessWorkflowRuntime,
        I주문업무Runtime, I음식점업무Runtime, I배차업무Runtime,
        I배송업무Runtime, I창고업무Runtime
    {
        private readonly ISimulationFoodOrderRuntime foodOrders;
        private readonly ISimulationLogisticsRuntime logistics;
        private readonly BusinessWorkflowRuntimeDescriptor descriptor;

        public BusinessWorkflowRuntime(
            ISimulationFoodOrderRuntime foodOrders,
            ISimulationLogisticsRuntime logistics,
            IBusinessWorkflowRuleEngine rules,
            BusinessWorkflowRuntimeDescriptor descriptor)
        {
            this.foodOrders = foodOrders ?? throw new ArgumentNullException(nameof(foodOrders));
            this.logistics = logistics ?? throw new ArgumentNullException(nameof(logistics));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.descriptor = ValidateAndCopy(descriptor);
        }

        public BusinessWorkflowRuntimeDescriptor Descriptor => Copy(descriptor);
        public IBusinessWorkflowRuleEngine Rules { get; }
        public I주문업무Runtime Orders => this;
        public I음식점업무Runtime Restaurants => this;
        public I배차업무Runtime Dispatch => this;
        public I배송업무Runtime Delivery => this;
        public I창고업무Runtime Warehouse => this;

        public ValueTask<경영SimulationSessionSnapshot> ConfirmRestaurantResponseAsync(
            string sessionStableId, Simulation음식점응답Request request,
            CancellationToken token = default)
            => foodOrders.ConfirmRestaurantResponseAsync(sessionStableId, request, token);

        public ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달PreviewRequest request,
            CancellationToken token = default)
            => foodOrders.PreviewFoodDeliveryAsync(sessionStableId, request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달ConfirmRequest request,
            CancellationToken token = default)
            => foodOrders.ConfirmFoodDeliveryAsync(sessionStableId, request, token);

        public ValueTask<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령PreviewRequest request,
            CancellationToken token = default)
            => foodOrders.PreviewFoodDeliveryReceiptAsync(sessionStableId, request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령ConfirmRequest request,
            CancellationToken token = default)
            => foodOrders.ConfirmFoodDeliveryReceiptAsync(sessionStableId, request, token);

        public ValueTask<SimulationFreightDispatchPreviewSnapshot> PreviewFreightDispatchAsync(
            string sessionStableId, SimulationFreightDispatchPreviewRequest request,
            CancellationToken token = default)
            => logistics.PreviewFreightDispatchAsync(sessionStableId, request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFreightDispatchAsync(
            string sessionStableId, SimulationFreightDispatchConfirmRequest request,
            CancellationToken token = default)
            => logistics.ConfirmFreightDispatchAsync(sessionStableId, request, token);

        public ValueTask<SimulationLogisticsMovementPreviewSnapshot> PreviewLogisticsMovementAsync(
            string sessionStableId, SimulationLogisticsMovementPreviewRequest request,
            CancellationToken token = default)
            => logistics.PreviewLogisticsMovementAsync(sessionStableId, request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmLogisticsMovementAsync(
            string sessionStableId, SimulationLogisticsMovementConfirmRequest request,
            CancellationToken token = default)
            => logistics.ConfirmLogisticsMovementAsync(sessionStableId, request, token);

        private static BusinessWorkflowRuntimeDescriptor ValidateAndCopy(
            BusinessWorkflowRuntimeDescriptor value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrWhiteSpace(value.RuntimeStableId))
                throw new ArgumentException("업무 흐름 Runtime 식별자가 필요합니다.", nameof(value));
            if (value.ModeCode != BusinessWorkflowRuntimeModeCodes.LocalProcess
                && value.ModeCode != BusinessWorkflowRuntimeModeCodes.RemoteHost)
                throw new ArgumentException("업무 흐름 Runtime 실행 위치가 올바르지 않습니다.", nameof(value));
            if (value.RequiresNetwork
                != (value.ModeCode == BusinessWorkflowRuntimeModeCodes.RemoteHost))
                throw new ArgumentException("업무 흐름 Runtime 네트워크 경계가 실행 위치와 다릅니다.", nameof(value));
            if (value.AuthorityScopeCode != BusinessWorkflowAuthorityScopeCodes.SimulationSession)
                throw new ArgumentException("업무 흐름 Runtime은 Simulation Session 권위만 사용할 수 있습니다.", nameof(value));
            if (value.ExperienceRoleCode != BusinessWorkflowExperienceRoleCodes.AutonomousNpcWorld)
                throw new ArgumentException("업무 흐름 Runtime은 자율 NPC 세계 경험만 표현할 수 있습니다.", nameof(value));
            if (value.AllowsOperationalDriverActions)
                throw new ArgumentException("업무 흐름 Runtime은 실제 기사 업무 동작을 허용하지 않습니다.", nameof(value));
            if (!value.ObservationPresentationOnly)
                throw new ArgumentException("업무 흐름 Runtime은 관찰 표현 전용 경계를 명시해야 합니다.", nameof(value));
            if (string.IsNullOrWhiteSpace(value.ContractRevision))
                throw new ArgumentException("업무 흐름 Runtime 계약 판본이 필요합니다.", nameof(value));
            return Copy(value);
        }

        protected static BusinessWorkflowRuntimeDescriptor Copy(
            BusinessWorkflowRuntimeDescriptor value)
            => new BusinessWorkflowRuntimeDescriptor
            {
                RuntimeStableId = value.RuntimeStableId,
                ModeCode = value.ModeCode,
                RequiresNetwork = value.RequiresNetwork,
                AuthorityScopeCode = value.AuthorityScopeCode,
                ExperienceRoleCode = value.ExperienceRoleCode,
                AllowsOperationalDriverActions = value.AllowsOperationalDriverActions,
                ObservationPresentationOnly = value.ObservationPresentationOnly,
                ContractRevision = value.ContractRevision,
                ClassificationMetadata = WorkflowClassificationMetadataCloner.Copy(
                    value.ClassificationMetadata),
            };
    }

    public static class WorkflowClassificationMetadataCloner
    {
        public static WorkflowClassificationMetadata? Copy(
            WorkflowClassificationMetadata? source)
            => source == null ? null : new WorkflowClassificationMetadata
            {
                SchemeCode = source.SchemeCode,
                ScopeCode = source.ScopeCode,
                PrimaryCode = source.PrimaryCode,
                SupportCodes = source.SupportCodes == null
                    ? Array.Empty<string>() : (string[])source.SupportCodes.Clone(),
                ElementCode = source.ElementCode,
                DisplayToken = source.DisplayToken,
                MeaningRevision = source.MeaningRevision,
                SourceStableIds = source.SourceStableIds == null
                    ? Array.Empty<string>() : (string[])source.SourceStableIds.Clone(),
                IsExecutionAuthority = false,
            };
    }
}
