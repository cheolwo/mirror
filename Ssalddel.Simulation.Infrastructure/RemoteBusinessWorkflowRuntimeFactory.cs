using System;
using System.Net.Http;
using System.Text.Json;
using Ssalddel.Simulation.BusinessWorkflow;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>원격 Simulation Host의 가상 세션을 관찰하는 업무 흐름 Runtime 조립기다.</summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "RemoteHost의 기존 Simulation HTTP 포트를 공통 업무 흐름 Runtime으로 조립한다.",
        Boundary = "원격 오류를 LocalProcess로 자동 전환하거나 서버 권위 상태를 복제하지 않는다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
    public static class RemoteBusinessWorkflowRuntimeFactory
    {
        public static IBusinessWorkflowRuntime Create(
            HttpClient client,
            IBusinessWorkflowRuleEngine? rules = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            var selectedRules = rules ?? BusinessWorkflowRuleEngine.기본;
            return new BusinessWorkflowRuntime(
                new RemoteSimulationFoodOrderRuntime(client, jsonOptions),
                new RemoteSimulationLogisticsRuntime(client, jsonOptions),
                selectedRules,
                new BusinessWorkflowRuntimeDescriptor
                {
                    RuntimeStableId = "business-workflow-runtime:remote-host",
                    ModeCode = BusinessWorkflowRuntimeModeCodes.RemoteHost,
                    RequiresNetwork = true,
                    AuthorityScopeCode = BusinessWorkflowAuthorityScopeCodes.SimulationSession,
                    ExperienceRoleCode = BusinessWorkflowExperienceRoleCodes.AutonomousNpcWorld,
                    AllowsOperationalDriverActions = false,
                    ObservationPresentationOnly = true,
                    ContractRevision = LocalBusinessWorkflowRuntimeFactory.ContractRevision,
                    ClassificationMetadata =
                        selectedRules.Engine정보조회().ClassificationMetadata,
                });
        }
    }
}
