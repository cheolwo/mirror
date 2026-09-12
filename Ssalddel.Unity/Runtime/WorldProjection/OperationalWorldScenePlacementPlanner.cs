using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public static class OperationalWorldPlacementDiagnosticCodes
    {
        public const string ApplyResultRejected = "ApplyResultRejected";
        public const string OperatingSystemUnsupported = "OperatingSystemUnsupported";
        public const string ItemKindUnsupported = "ItemKindUnsupported";
        public const string DataPolicyUnsupported = "DataPolicyUnsupported";
        public const string LocalPersistenceForbidden = "LocalPersistenceForbidden";
    }

    public sealed class OperationalWorldPlacementInstruction
    {
        public string ObjectStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public string OperatingSystemId { get; set; } = string.Empty;
        public string AnchorKey { get; set; } = string.Empty;
        public string VisualKey { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public string WorkStableId { get; set; } = string.Empty;
        public string LifecycleStageCode { get; set; } = string.Empty;
        public string AttentionStateCode { get; set; } = string.Empty;
        public string ObjectKindCode { get; set; } = string.Empty;
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string SourceKindCode { get; set; } = string.Empty;
        public string ScenarioRunStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public sealed class OperationalWorldPlacementDiagnostic
    {
        public string SnapshotStableId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class OperationalWorldPlacementPlan
    {
        public bool Accepted { get; set; }
        public OperationalWorldPlacementInstruction[] Instructions { get; set; } =
            Array.Empty<OperationalWorldPlacementInstruction>();
        public OperationalWorldPlacementDiagnostic[] Diagnostics { get; set; } =
            Array.Empty<OperationalWorldPlacementDiagnostic>();
    }

    /// <summary>
    /// 해석을 마친 운영 상태 사본을 SimulationWorldShell 배치 준비 명령으로 좁힙니다.
    /// 좌표·개인정보·표현 JSON을 복사하지 않으며 Scene 또는 GameObject를 직접 생성하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "운영 상태 사본을 개인정보 없는 Unity 배치 준비 명령으로 변환한다.",
        Boundary = "순수 C# 계획이며 실제 Scene·GameObject 배치나 운영 상태 변경 증거가 아니다.")]
    public sealed class OperationalWorldScenePlacementPlanner
    {
        private static readonly IReadOnlyDictionary<string, string> AnchorByOperatingSystem =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OperationalWorldOperatingSystemIds.FoodDelivery] = "world.area.food-delivery",
                [OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment] = "world.area.warehouse",
                [OperationalWorldOperatingSystemIds.DomesticCargoTransport] = "world.area.cargo",
                [OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics] = "world.area.mart"
            };

        private static readonly IReadOnlyDictionary<string, string> VisualByItemKind =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OperationalWorldSceneItemKinds.CompletedLifecycle] = "operational.completed-lifecycle",
                [OperationalWorldSceneItemKinds.WarehouseTask] = "warehouse.task",
                [OperationalWorldSceneItemKinds.WarehouseActor] = "warehouse.actor",
                [OperationalWorldSceneItemKinds.CargoHandoff] = "cargo.handoff"
            };

        public OperationalWorldPlacementPlan Create(OperationalWorldSceneApplyResult applyResult)
        {
            if (applyResult == null) throw new ArgumentNullException(nameof(applyResult));
            if (!applyResult.Accepted)
            {
                return new OperationalWorldPlacementPlan
                {
                    Accepted = false,
                    Diagnostics = new[]
                    {
                        new OperationalWorldPlacementDiagnostic
                        {
                            Code = OperationalWorldPlacementDiagnosticCodes.ApplyResultRejected
                        }
                    }
                };
            }

            var instructions = new List<OperationalWorldPlacementInstruction>();
            var diagnostics = new List<OperationalWorldPlacementDiagnostic>();
            foreach (var item in applyResult.CurrentItems ?? Array.Empty<OperationalWorldSceneItem>())
            {
                var diagnosticCode = Validate(item);
                if (!string.IsNullOrEmpty(diagnosticCode))
                {
                    diagnostics.Add(new OperationalWorldPlacementDiagnostic
                    {
                        SnapshotStableId = item.SnapshotStableId,
                        Code = diagnosticCode
                    });
                    continue;
                }

                instructions.Add(new OperationalWorldPlacementInstruction
                {
                    ObjectStableId = item.SnapshotStableId,
                    AreaStableId = item.AreaStableId,
                    OperatingSystemId = item.OperatingSystemId,
                    AnchorKey = AnchorByOperatingSystem[item.OperatingSystemId],
                    VisualKey = VisualByItemKind[item.ItemKind],
                    ActivityCode = item.ActivityCode,
                    WorkStableId = string.IsNullOrWhiteSpace(item.WorkStableId)
                        ? item.SnapshotStableId : item.WorkStableId,
                    LifecycleStageCode = string.IsNullOrWhiteSpace(item.LifecycleStageCode)
                        ? item.ActivityCode : item.LifecycleStageCode,
                    AttentionStateCode = item.AttentionStateCode,
                    ObjectKindCode = string.IsNullOrWhiteSpace(item.ObjectKindCode)
                        ? item.ItemKind : item.ObjectKindCode,
                    SemanticPlaceStableId = item.SemanticPlaceStableId,
                    SourceKindCode = item.SourceKindCode,
                    ScenarioRunStableId = item.ScenarioRunStableId,
                    Revision = item.Revision,
                    ExpiresAtUtc = item.ExpiresAtUtc
                });
            }

            return new OperationalWorldPlacementPlan
            {
                Accepted = true,
                Instructions = instructions
                    .OrderBy(x => x.AnchorKey, StringComparer.Ordinal)
                    .ThenBy(x => x.ObjectStableId, StringComparer.Ordinal)
                    .ToArray(),
                Diagnostics = diagnostics
                    .OrderBy(x => x.SnapshotStableId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        private static string Validate(OperationalWorldSceneItem item)
        {
            if (!AnchorByOperatingSystem.ContainsKey(item.OperatingSystemId))
                return OperationalWorldPlacementDiagnosticCodes.OperatingSystemUnsupported;
            if (!VisualByItemKind.ContainsKey(item.ItemKind))
                return OperationalWorldPlacementDiagnosticCodes.ItemKindUnsupported;
            if (!string.Equals(item.DataPolicyCode, OperationalWorldScenePolicy.OnlineEphemeral, StringComparison.Ordinal))
                return OperationalWorldPlacementDiagnosticCodes.DataPolicyUnsupported;
            if (item.LocalStorageAllowed || item.ReplayAllowed)
                return OperationalWorldPlacementDiagnosticCodes.LocalPersistenceForbidden;
            return string.Empty;
        }
    }
}
