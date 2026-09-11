using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;
using Canonical = Ssalddel.Simulation.BusinessWorkflow;

namespace Ssalddel.BusinessWorkflow
{
    /// <summary>
    /// 기존 namespace 소비자를 위한 호환 facade다. 새 코드는
    /// Ssalddel.Simulation.BusinessWorkflow.BusinessWorkflowRuntime을 사용한다.
    /// </summary>
    public sealed class BusinessWorkflowRuntime : Canonical.BusinessWorkflowRuntime,
        IBusinessWorkflowRuntime, I주문업무Runtime, I음식점업무Runtime,
        I배차업무Runtime, I배송업무Runtime, I창고업무Runtime
    {
        public BusinessWorkflowRuntime(
            ISimulationFoodOrderRuntime foodOrders,
            ISimulationLogisticsRuntime logistics,
            IBusinessWorkflowRuleEngine rules,
            BusinessWorkflowRuntimeDescriptor descriptor)
            : base(foodOrders, logistics, rules, descriptor)
        {
        }

        public new BusinessWorkflowRuntimeDescriptor Descriptor
        {
            get
            {
                var value = base.Descriptor;
                return new BusinessWorkflowRuntimeDescriptor
                {
                    RuntimeStableId = value.RuntimeStableId,
                    ModeCode = value.ModeCode,
                    RequiresNetwork = value.RequiresNetwork,
                    AuthorityScopeCode = value.AuthorityScopeCode,
                    ExperienceRoleCode = value.ExperienceRoleCode,
                    AllowsOperationalDriverActions = value.AllowsOperationalDriverActions,
                    ObservationPresentationOnly = value.ObservationPresentationOnly,
                    ContractRevision = value.ContractRevision,
                    ClassificationMetadata = Canonical.WorkflowClassificationMetadataCloner.Copy(
                        value.ClassificationMetadata),
                };
            }
        }

        public new I주문업무Runtime Orders => this;
        public new I음식점업무Runtime Restaurants => this;
        public new I배차업무Runtime Dispatch => this;
        public new I배송업무Runtime Delivery => this;
        public new I창고업무Runtime Warehouse => this;
    }

    public static class WorkflowClassificationMetadataCloner
    {
        public static WorkflowClassificationMetadata? Copy(
            WorkflowClassificationMetadata? source)
            => Canonical.WorkflowClassificationMetadataCloner.Copy(source);
    }
}
