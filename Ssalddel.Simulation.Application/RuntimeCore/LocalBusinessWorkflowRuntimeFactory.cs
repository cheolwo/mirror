using System;
using Ssalddel.Simulation.BusinessWorkflow;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Application
{
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "LocalProcess의 기존 Simulation Core를 공통 업무 흐름 Runtime으로 조립한다.",
        Boundary = "새 상태 권위를 만들지 않고 같은 LocalSimulationRuntime과 Session을 공유한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter)]
    public static class LocalBusinessWorkflowRuntimeFactory
    {
        public const string ContractRevision = "business-workflow-runtime.r2";

        public static IBusinessWorkflowRuntime Create(
            LocalSimulationRuntime runtime,
            IBusinessWorkflowRuleEngine rules)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            return new BusinessWorkflowRuntime(
                runtime,
                runtime,
                rules,
                new BusinessWorkflowRuntimeDescriptor
                {
                    RuntimeStableId = runtime.Descriptor.RuntimeStableId,
                    ModeCode = BusinessWorkflowRuntimeModeCodes.LocalProcess,
                    RequiresNetwork = false,
                    AuthorityScopeCode = BusinessWorkflowAuthorityScopeCodes.SimulationSession,
                    ExperienceRoleCode = BusinessWorkflowExperienceRoleCodes.AutonomousNpcWorld,
                    AllowsOperationalDriverActions = false,
                    ObservationPresentationOnly = true,
                    ContractRevision = ContractRevision,
                    ClassificationMetadata =
                        rules.Engine정보조회().ClassificationMetadata,
                });
        }
    }
}
