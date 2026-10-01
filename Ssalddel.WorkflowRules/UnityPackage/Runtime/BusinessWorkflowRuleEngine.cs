using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WorkflowRules
{
    /// <summary>
    /// 주문·음식점·배차·배송·창고의 상태 전이 규칙을 조립한다.
    /// 분류 메타데이터는 결과 설명에만 붙으며 판정에는 사용하지 않는다.
    /// </summary>
    public sealed class BusinessWorkflowRuleEngine : IBusinessWorkflowRuleEngine
    {
        private readonly IBusinessWorkflowRuleModule[] modules;
        private static readonly BusinessWorkflowEngineDescriptor Descriptor =
            new BusinessWorkflowEngineDescriptor
            {
                EngineCode = "BusinessWorkflow",
                DisplayName = "업무 흐름 규칙 엔진",
                MeaningRevision = "business-workflow-engine.r1",
                SourceStableIds = new[]
                {
                    "workflow-engine:business.r1",
                    "meaning-revision:business-workflow-engine.r1",
                },
                ClassificationMetadata = Classification(
                    "Engine", FiveElementClassificationCodes.토간,
                    "EARTH", "role.gan.earth",
                    new[] { FiveElementClassificationCodes.수감 }),
                IsExecutionAuthority = false,
            };

        public static IBusinessWorkflowRuleEngine 기본 { get; } =
            new BusinessWorkflowRuleEngine(CreateDefaultModules());

        public BusinessWorkflowRuleEngine(
            IEnumerable<IBusinessWorkflowRuleModule> modules)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            this.modules = modules.ToArray();
            if (this.modules.Length == 0)
                throw new ArgumentException("업무 흐름 모듈이 하나 이상 필요합니다.", nameof(modules));
            if (this.modules.Any(module => module == null))
                throw new ArgumentException("업무 흐름 모듈은 null일 수 없습니다.", nameof(modules));
            if (this.modules.Select(module => module.Descriptor.ModuleCode)
                .Distinct(StringComparer.Ordinal).Count() != this.modules.Length)
                throw new ArgumentException("업무 흐름 모듈 코드는 중복될 수 없습니다.", nameof(modules));
        }

        public BusinessWorkflowModuleDescriptor[] 전체Module조회()
            => modules.Select(module => Clone(module.Descriptor)).ToArray();

        public BusinessWorkflowEngineDescriptor Engine정보조회()
            => Clone(Descriptor);

        public BusinessWorkflowTransitionDecision 판정(
            BusinessWorkflowTransitionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestedModule = request.요청ModuleCode?.Trim() ?? string.Empty;
            IBusinessWorkflowRuleModule? module;
            if (requestedModule.Length > 0)
            {
                module = modules.FirstOrDefault(candidate => string.Equals(
                    candidate.Descriptor.ModuleCode, requestedModule,
                    StringComparison.Ordinal));
                if (module == null)
                    return Blocked(request, string.Empty,
                        BusinessWorkflowRuleBlockReasonCodes.Module미확인);

                if (!module.처리대상인가(request))
                    return Blocked(request, module.Descriptor.ModuleCode,
                        BusinessWorkflowRuleBlockReasonCodes.Module불일치,
                        module.Descriptor);
            }
            else
            {
                module = modules.FirstOrDefault(candidate => candidate.처리대상인가(request));
                if (module == null)
                {
                    var legacy = 업무상태전이평가기.판정(
                        request.업무흐름코드,
                        request.현재상태코드,
                        request.목표상태코드);
                    return FromLegacy(legacy, null);
                }
            }

            return module.판정(request);
        }

        private static IBusinessWorkflowRuleModule[] CreateDefaultModules()
            => new IBusinessWorkflowRuleModule[]
            {
                Module(BusinessWorkflowModuleCodes.주문, "주문",
                    FiveElementClassificationCodes.목진, "WOOD", "role.jin.wood",
                    "order-workflow-meaning.r1", "workflow-meaning:order.r1",
                    업무흐름코드.개별주문, 업무흐름코드.같이주문, 업무흐름코드.음식배달),
                Module(BusinessWorkflowModuleCodes.음식점, "음식점 운영",
                    FiveElementClassificationCodes.화리, "FIRE", "role.ri.fire",
                    "restaurant-workflow-meaning.r1", "workflow-meaning:restaurant.r1",
                    업무흐름코드.음식배달),
                Module(BusinessWorkflowModuleCodes.배차, "배차",
                    FiveElementClassificationCodes.토간, "EARTH", "role.gan.earth",
                    "dispatch-workflow-meaning.r2", "workflow-meaning:dispatch.r1",
                    업무흐름코드.음식배달, 업무흐름코드.화물운송),
                Module(BusinessWorkflowModuleCodes.배송, "배달·운송",
                    FiveElementClassificationCodes.수감, "WATER", "role.gam.water",
                    "delivery-workflow-meaning.r1", "workflow-meaning:delivery.r1",
                    업무흐름코드.음식배달, 업무흐름코드.화물운송),
                Module(BusinessWorkflowModuleCodes.창고, "창고 운영",
                    FiveElementClassificationCodes.금태, "METAL", "role.tae.metal",
                    "warehouse-workflow-meaning.r1", "workflow-meaning:warehouse.r1",
                    업무흐름코드.창고입고),
            };

        private static IBusinessWorkflowRuleModule Module(
            string moduleCode,
            string displayName,
            string primaryClassificationCode,
            string elementCode,
            string displayToken,
            string meaningRevision,
            string profileStableId,
            params string[] workflowCodes)
            => new DefaultBusinessWorkflowRuleModule(
                new BusinessWorkflowModuleDescriptor
                {
                    ModuleCode = moduleCode,
                    DisplayName = displayName,
                    MeaningRevision = meaningRevision,
                    WorkflowCodes = workflowCodes,
                    SourceStableIds = new[]
                    {
                        profileStableId,
                        "meaning-revision:" + meaningRevision,
                    },
                    ClassificationMetadata = Classification(
                        "Module:" + moduleCode,
                        primaryClassificationCode,
                        elementCode,
                        displayToken,
                        Array.Empty<string>()),
                    IsExecutionAuthority = false,
                });

        private static WorkflowClassificationMetadata Classification(
            string scopeCode,
            string primaryCode,
            string elementCode,
            string displayToken,
            string[] supportCodes)
            => new WorkflowClassificationMetadata
            {
                SchemeCode = WorkflowClassificationSchemeCodes.FiveElementGwae,
                ScopeCode = scopeCode,
                PrimaryCode = primaryCode,
                SupportCodes = supportCodes,
                ElementCode = elementCode,
                DisplayToken = displayToken,
                MeaningRevision = "five-element-workflow-engine-meaning.r1",
                SourceStableIds = new[]
                {
                    "workflow-classification:five-element.r1",
                    "classification-ledger:world-interaction-gwae.r10",
                },
                IsExecutionAuthority = false,
            };

        private static string ResolveModuleCode(BusinessWorkflowTransitionRequest request)
        {
            var workflow = request.업무흐름코드?.Trim() ?? string.Empty;
            var target = request.목표상태코드?.Trim() ?? string.Empty;
            var current = request.현재상태코드?.Trim() ?? string.Empty;

            if (string.Equals(workflow, 업무흐름코드.개별주문, StringComparison.Ordinal)
                || string.Equals(workflow, 업무흐름코드.같이주문, StringComparison.Ordinal))
                return BusinessWorkflowModuleCodes.주문;
            if (string.Equals(workflow, 업무흐름코드.창고입고, StringComparison.Ordinal))
                return BusinessWorkflowModuleCodes.창고;
            if (string.Equals(workflow, 업무흐름코드.화물운송, StringComparison.Ordinal))
                return IsDispatchState(target.Length > 0 ? target : current)
                    ? BusinessWorkflowModuleCodes.배차
                    : BusinessWorkflowModuleCodes.배송;
            if (!string.Equals(workflow, 업무흐름코드.음식배달, StringComparison.Ordinal))
                return string.Empty;

            var state = target.Length > 0 ? target : current;
            if (string.Equals(state, 음식배달상태코드.주문확인, StringComparison.Ordinal)
                || string.Equals(state, 음식배달상태코드.조리중, StringComparison.Ordinal)
                || string.Equals(state, 음식배달상태코드.픽업대기, StringComparison.Ordinal)
                || string.Equals(state, 음식배달상태코드.거절, StringComparison.Ordinal))
                return BusinessWorkflowModuleCodes.음식점;
            if (string.Equals(state, 음식배달상태코드.기사배정, StringComparison.Ordinal))
                return BusinessWorkflowModuleCodes.배차;
            if (string.Equals(state, 음식배달상태코드.픽업완료, StringComparison.Ordinal)
                || string.Equals(state, 음식배달상태코드.전달완료, StringComparison.Ordinal))
                return BusinessWorkflowModuleCodes.배송;
            return BusinessWorkflowModuleCodes.주문;
        }

        private static bool IsDispatchState(string state)
            => string.Equals(state, 화물운송상태코드.배차대기, StringComparison.Ordinal)
                || string.Equals(state, 화물운송상태코드.배차대기확정, StringComparison.Ordinal)
                || string.Equals(state, 화물운송상태코드.매칭중, StringComparison.Ordinal)
                || string.Equals(state, 화물운송상태코드.배차확정, StringComparison.Ordinal);

        private static BusinessWorkflowTransitionDecision Blocked(
            BusinessWorkflowTransitionRequest request,
            string moduleCode,
            string reason,
            BusinessWorkflowModuleDescriptor? descriptor = null)
        {
            업무상태전이판정 legacy;
            try
            {
                legacy = 업무상태전이평가기.판정(
                    request.업무흐름코드,
                    request.현재상태코드,
                    request.목표상태코드);
            }
            catch (ArgumentException)
            {
                legacy = new 업무상태전이판정();
            }

            return new BusinessWorkflowTransitionDecision
            {
                판정ModuleCode = moduleCode,
                MeaningRevision = descriptor?.MeaningRevision ?? string.Empty,
                RuleRevision = legacy.RuleRevision,
                차단사유코드목록 = new[] { reason },
                SourceStableIds = (descriptor?.SourceStableIds ?? Array.Empty<string>())
                    .Concat(legacy.SourceStableIds).Distinct(StringComparer.Ordinal).ToArray(),
                ClassificationMetadata = Clone(descriptor?.ClassificationMetadata),
            };
        }

        private static BusinessWorkflowTransitionDecision FromLegacy(
            업무상태전이판정 legacy,
            BusinessWorkflowModuleDescriptor? descriptor)
            => new BusinessWorkflowTransitionDecision
            {
                허용여부 = legacy.허용여부,
                멱등재시도여부 = legacy.멱등재시도여부,
                판정ModuleCode = descriptor?.ModuleCode ?? string.Empty,
                MeaningRevision = descriptor?.MeaningRevision ?? string.Empty,
                RuleRevision = legacy.RuleRevision,
                차단사유코드목록 = legacy.차단사유코드목록.ToArray(),
                SourceStableIds = (descriptor?.SourceStableIds ?? Array.Empty<string>())
                    .Concat(legacy.SourceStableIds).Distinct(StringComparer.Ordinal).ToArray(),
                ClassificationMetadata = Clone(descriptor?.ClassificationMetadata),
            };

        private static BusinessWorkflowModuleDescriptor Clone(
            BusinessWorkflowModuleDescriptor source)
            => new BusinessWorkflowModuleDescriptor
            {
                ModuleCode = source.ModuleCode,
                DisplayName = source.DisplayName,
                MeaningRevision = source.MeaningRevision,
                WorkflowCodes = source.WorkflowCodes.ToArray(),
                SourceStableIds = source.SourceStableIds.ToArray(),
                ClassificationMetadata = Clone(source.ClassificationMetadata),
                IsExecutionAuthority = source.IsExecutionAuthority,
            };

        private static BusinessWorkflowEngineDescriptor Clone(
            BusinessWorkflowEngineDescriptor source)
            => new BusinessWorkflowEngineDescriptor
            {
                EngineCode = source.EngineCode,
                DisplayName = source.DisplayName,
                MeaningRevision = source.MeaningRevision,
                SourceStableIds = source.SourceStableIds.ToArray(),
                ClassificationMetadata = Clone(source.ClassificationMetadata),
                IsExecutionAuthority = source.IsExecutionAuthority,
            };

        internal static WorkflowClassificationMetadata? Clone(
            WorkflowClassificationMetadata? source)
            => source == null ? null : new WorkflowClassificationMetadata
            {
                SchemeCode = source.SchemeCode,
                ScopeCode = source.ScopeCode,
                PrimaryCode = source.PrimaryCode,
                SupportCodes = source.SupportCodes?.ToArray() ?? Array.Empty<string>(),
                ElementCode = source.ElementCode,
                DisplayToken = source.DisplayToken,
                MeaningRevision = source.MeaningRevision,
                SourceStableIds = source.SourceStableIds?.ToArray() ?? Array.Empty<string>(),
                IsExecutionAuthority = false,
            };

        private sealed class DefaultBusinessWorkflowRuleModule
            : IBusinessWorkflowRuleModule
        {
            public DefaultBusinessWorkflowRuleModule(
                BusinessWorkflowModuleDescriptor descriptor)
                => Descriptor = descriptor
                    ?? throw new ArgumentNullException(nameof(descriptor));

            public BusinessWorkflowModuleDescriptor Descriptor { get; }

            public bool 처리대상인가(BusinessWorkflowTransitionRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                return string.Equals(ResolveModuleCode(request), Descriptor.ModuleCode,
                    StringComparison.Ordinal);
            }

            public BusinessWorkflowTransitionDecision 판정(
                BusinessWorkflowTransitionRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                var legacy = 업무상태전이평가기.판정(
                    request.업무흐름코드,
                    request.현재상태코드,
                    request.목표상태코드);
                return FromLegacy(legacy, Descriptor);
            }
        }
    }

    internal static class 업무상태전이평가기
    {
        internal static 업무상태전이판정 판정(
            string 업무코드,
            string 현재상태코드,
            string 목표상태코드)
        {
            업무흐름규칙Snapshot rule;
            try
            {
                rule = 업무흐름규칙Catalog.조회(업무코드);
            }
            catch (ArgumentException)
            {
                return Blocked(업무규칙차단사유코드.지원하지않는업무);
            }

            var current = 현재상태코드?.Trim() ?? string.Empty;
            var target = 목표상태코드?.Trim() ?? string.Empty;
            if (!rule.상태코드목록.Contains(current, StringComparer.Ordinal))
                return Blocked(rule, 업무규칙차단사유코드.알수없는현재상태);

            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                return new 업무상태전이판정
                {
                    허용여부 = true,
                    멱등재시도여부 = true,
                    RuleRevision = rule.RuleRevision,
                    SourceStableIds = rule.SourceStableIds.ToArray(),
                };
            }

            var allowed = rule.허용전이목록.Any(transition =>
                string.Equals(transition.현재상태코드, current, StringComparison.Ordinal)
                && string.Equals(transition.목표상태코드, target, StringComparison.Ordinal));
            return allowed
                ? new 업무상태전이판정
                {
                    허용여부 = true,
                    RuleRevision = rule.RuleRevision,
                    SourceStableIds = rule.SourceStableIds.ToArray(),
                }
                : Blocked(rule, 업무규칙차단사유코드.허용되지않은상태전이);
        }

        private static 업무상태전이판정 Blocked(string reason)
            => new 업무상태전이판정
            {
                차단사유코드목록 = new[] { reason },
            };

        private static 업무상태전이판정 Blocked(
            업무흐름규칙Snapshot rule,
            string reason)
            => new 업무상태전이판정
            {
                RuleRevision = rule.RuleRevision,
                SourceStableIds = rule.SourceStableIds.ToArray(),
                차단사유코드목록 = new[] { reason },
            };
    }
}
