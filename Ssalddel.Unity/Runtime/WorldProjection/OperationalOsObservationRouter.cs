using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public static class OperationalOsAttentionStateCodes
    {
        public const string Completed = "Completed";
    }

    public static class OperationalOsObservationDiagnosticCodes
    {
        public const string ApplyResultRejected = "ApplyResultRejected";
        public const string OperatingSystemUnsupported = "OperatingSystemUnsupported";
        public const string ItemKindUnsupported = "ItemKindUnsupported";
        public const string DataPolicyUnsupported = "DataPolicyUnsupported";
        public const string LocalPersistenceForbidden = "LocalPersistenceForbidden";
        public const string ActivityUnsupported = "ActivityUnsupported";
    }

    /// <summary>
    /// 운영 서버의 상태 사본을 Unity가 표현하기 위해 메모리에서만 보관하는 읽기 전용 관찰 상태입니다.
    /// 운영 Command, 저장 또는 재생 계약을 제공하지 않습니다.
    /// </summary>
    public sealed class OperationalOsObservationState
    {
        public string OperatingSystemId { get; set; } = string.Empty;
        public string WorkStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string LifecycleStageId { get; set; } = string.Empty;
        public string AttentionStateCode { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string RepresentationDataJson { get; set; } = "{}";
    }

    public sealed class OperationalOsObservationDiagnostic
    {
        public string OperatingSystemId { get; set; } = string.Empty;
        public string SnapshotStableId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class OperationalOsObservationRouteResult
    {
        public bool Accepted { get; set; }
        public OperationalOsObservationState[] CurrentStates { get; set; } =
            Array.Empty<OperationalOsObservationState>();
        public OperationalOsObservationDiagnostic[] Diagnostics { get; set; } =
            Array.Empty<OperationalOsObservationDiagnostic>();
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "운영 상태 사본을 OS별 Unity 메모리 관찰 상태로 변환하는 Adapter 경계를 제공한다.",
        Boundary = "서버 권위 변경, 로컬 저장, Scene 또는 GameObject 생성 책임이 아니다.")]
    public interface IOperationalOsObservationAdapter
    {
        string OperatingSystemId { get; }

        OperationalOsObservationState? Adapt(
            OperationalWorldSceneItem item,
            out string diagnosticCode);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "FoodDeliveryOS 정상 완료 사본의 안전 조건을 검사해 메모리 관찰 상태로 변환한다.",
        Boundary = "완료를 재판정하거나 운영 Command, Scene, GameObject를 생성하지 않는다.")]
    public sealed class FoodDeliveryOsObservationAdapter : IOperationalOsObservationAdapter
    {
        public string OperatingSystemId => OperationalWorldOperatingSystemIds.FoodDelivery;

        public OperationalOsObservationState? Adapt(
            OperationalWorldSceneItem item,
            out string diagnosticCode)
        {
            if (!string.Equals(item.ItemKind, OperationalWorldSceneItemKinds.CompletedLifecycle, StringComparison.Ordinal))
                return Rejected(OperationalOsObservationDiagnosticCodes.ItemKindUnsupported, out diagnosticCode);
            if (!string.Equals(item.DataPolicyCode, OperationalWorldScenePolicy.OnlineEphemeral, StringComparison.Ordinal))
                return Rejected(OperationalOsObservationDiagnosticCodes.DataPolicyUnsupported, out diagnosticCode);
            if (item.LocalStorageAllowed || item.ReplayAllowed)
                return Rejected(OperationalOsObservationDiagnosticCodes.LocalPersistenceForbidden, out diagnosticCode);
            if (!string.Equals(item.ActivityCode, 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed, StringComparison.Ordinal))
                return Rejected(OperationalOsObservationDiagnosticCodes.ActivityUnsupported, out diagnosticCode);

            diagnosticCode = string.Empty;
            return new OperationalOsObservationState
            {
                OperatingSystemId = OperatingSystemId,
                WorkStableId = item.SnapshotStableId,
                AreaStableId = item.AreaStableId,
                Revision = item.Revision,
                LifecycleStageId = item.ActivityCode,
                AttentionStateCode = OperationalOsAttentionStateCodes.Completed,
                RoleCode = item.RoleCode,
                OccurredAtUtc = item.OccurredAtUtc,
                ExpiresAtUtc = item.ExpiresAtUtc,
                RepresentationDataJson = item.RepresentationDataJson
            };
        }

        private static OperationalOsObservationState? Rejected(
            string code,
            out string diagnosticCode)
        {
            diagnosticCode = code;
            return null;
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "한 OS의 현재 관찰 상태를 Unity 프로세스 메모리에서 교체 보관한다.",
        Boundary = "파일, PlayerPrefs, Save 또는 서버 상태 저장 근거가 아니다.")]
    public sealed class OperationalOsObservationModule
    {
        private OperationalOsObservationState[] currentStates =
            Array.Empty<OperationalOsObservationState>();

        public OperationalOsObservationModule(IOperationalOsObservationAdapter adapter)
        {
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            if (string.IsNullOrWhiteSpace(adapter.OperatingSystemId))
                throw new ArgumentException("OperationalOsIdRequired", nameof(adapter));
        }

        public IOperationalOsObservationAdapter Adapter { get; }

        public string OperatingSystemId => Adapter.OperatingSystemId;

        public OperationalOsObservationState[] CurrentStates => currentStates.ToArray();

        internal void Replace(IEnumerable<OperationalOsObservationState> states)
        {
            currentStates = states
                .OrderBy(state => state.WorkStableId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed class OperationalOsModuleRegistry
    {
        private readonly Dictionary<string, OperationalOsObservationModule> modules;

        public OperationalOsModuleRegistry(IEnumerable<OperationalOsObservationModule> modules)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            this.modules = new Dictionary<string, OperationalOsObservationModule>(StringComparer.Ordinal);
            foreach (var module in modules)
            {
                if (module == null) throw new ArgumentException("OperationalOsModuleNull", nameof(modules));
                if (this.modules.ContainsKey(module.OperatingSystemId))
                    throw new ArgumentException("OperationalOsModuleDuplicate:" + module.OperatingSystemId, nameof(modules));
                this.modules.Add(module.OperatingSystemId, module);
            }
        }

        public OperationalOsObservationModule[] Modules => modules.Values
            .OrderBy(module => module.OperatingSystemId, StringComparer.Ordinal)
            .ToArray();

        public bool TryGet(string operatingSystemId, out OperationalOsObservationModule module)
            => modules.TryGetValue(operatingSystemId, out module!);
    }

    /// <summary>
    /// 기존 Interpreter가 확정한 현재 메모리 상태를 OS별 관찰 모듈로 분배합니다.
    /// 누락·만료 제거는 Interpreter 결과를 그대로 따르며 서버나 Simulation 상태를 변경하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "운영 지역 상태 사본을 OperatingSystemId별 Unity 메모리 관찰 모듈로 분배한다.",
        Boundary = "순수 메모리 분배 시험이며 Scene, GameObject, Play Mode 또는 운영 Command 증거가 아니다.")]
    public sealed class OperationalOsObservationRouter
    {
        private readonly OperationalOsModuleRegistry registry;

        public OperationalOsObservationRouter(OperationalOsModuleRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public OperationalOsObservationRouteResult Route(OperationalWorldSceneApplyResult applyResult)
        {
            if (applyResult == null) throw new ArgumentNullException(nameof(applyResult));
            if (!applyResult.Accepted)
            {
                return new OperationalOsObservationRouteResult
                {
                    Accepted = false,
                    CurrentStates = Snapshot(),
                    Diagnostics = new[]
                    {
                        new OperationalOsObservationDiagnostic
                        {
                            Code = OperationalOsObservationDiagnosticCodes.ApplyResultRejected
                        }
                    }
                };
            }

            var statesByOs = registry.Modules.ToDictionary(
                module => module.OperatingSystemId,
                _ => new List<OperationalOsObservationState>(),
                StringComparer.Ordinal);
            var diagnostics = new List<OperationalOsObservationDiagnostic>();
            foreach (var item in applyResult.CurrentItems ?? Array.Empty<OperationalWorldSceneItem>())
            {
                if (!registry.TryGet(item.OperatingSystemId, out var module))
                {
                    diagnostics.Add(Diagnostic(item, OperationalOsObservationDiagnosticCodes.OperatingSystemUnsupported));
                    continue;
                }

                var state = module.Adapter.Adapt(item, out var diagnosticCode);
                if (state == null)
                {
                    diagnostics.Add(Diagnostic(item, diagnosticCode));
                    continue;
                }

                statesByOs[module.OperatingSystemId].Add(state);
            }

            foreach (var module in registry.Modules) module.Replace(statesByOs[module.OperatingSystemId]);
            return new OperationalOsObservationRouteResult
            {
                Accepted = true,
                CurrentStates = Snapshot(),
                Diagnostics = diagnostics.ToArray()
            };
        }

        private OperationalOsObservationState[] Snapshot()
            => registry.Modules
                .SelectMany(module => module.CurrentStates)
                .OrderBy(state => state.OperatingSystemId, StringComparer.Ordinal)
                .ThenBy(state => state.WorkStableId, StringComparer.Ordinal)
                .ToArray();

        private static OperationalOsObservationDiagnostic Diagnostic(
            OperationalWorldSceneItem item,
            string code)
            => new OperationalOsObservationDiagnostic
            {
                OperatingSystemId = item.OperatingSystemId,
                SnapshotStableId = item.SnapshotStableId,
                Code = code
            };
    }
}
