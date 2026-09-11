using Canonical = Ssalddel.Simulation.BusinessWorkflow;

namespace Ssalddel.BusinessWorkflow
{
    /// <summary>기존 소비자를 위한 호환 코드다. 새 코드는 Simulation 전용 namespace를 사용한다.</summary>
    public static class BusinessWorkflowRuntimeModeCodes
    {
        public const string LocalProcess = Canonical.BusinessWorkflowRuntimeModeCodes.LocalProcess;
        public const string RemoteHost = Canonical.BusinessWorkflowRuntimeModeCodes.RemoteHost;
    }

    public static class BusinessWorkflowAuthorityScopeCodes
    {
        public const string SimulationSession = Canonical.BusinessWorkflowAuthorityScopeCodes.SimulationSession;
    }

    public static class BusinessWorkflowExperienceRoleCodes
    {
        public const string AutonomousNpcWorld = Canonical.BusinessWorkflowExperienceRoleCodes.AutonomousNpcWorld;
    }

    public sealed class BusinessWorkflowRuntimeDescriptor
        : Canonical.BusinessWorkflowRuntimeDescriptor
    {
    }

    public interface I주문업무Runtime : Canonical.I주문업무Runtime
    {
    }

    public interface I음식점업무Runtime : Canonical.I음식점업무Runtime
    {
    }

    public interface I배차업무Runtime : Canonical.I배차업무Runtime
    {
    }

    public interface I배송업무Runtime : Canonical.I배송업무Runtime
    {
    }

    public interface I창고업무Runtime : Canonical.I창고업무Runtime
    {
    }

    public interface IBusinessWorkflowRuntime : Canonical.IBusinessWorkflowRuntime
    {
        new BusinessWorkflowRuntimeDescriptor Descriptor { get; }
        new I주문업무Runtime Orders { get; }
        new I음식점업무Runtime Restaurants { get; }
        new I배차업무Runtime Dispatch { get; }
        new I배송업무Runtime Delivery { get; }
        new I창고업무Runtime Warehouse { get; }
    }
}
