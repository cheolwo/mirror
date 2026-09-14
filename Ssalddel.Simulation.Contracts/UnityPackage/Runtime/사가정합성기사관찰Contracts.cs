using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>
    /// 사가정 디오라마의 첫 합성 기사 관찰 절편이 공유하는 고정 계약이다.
    /// 실제 사람, 운영 배차, 실제 정산을 나타내지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceCoverageExclusion(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceCoverageExclusionCategory.TechnicalHelper,
        "사가정 합성 기사 관찰 계약이 공유하는 고정 식별자와 상태 값 모음이며 독립 실행 책임을 갖지 않는다.")]
    public static class 사가정합성기사관찰Policy
    {
        public const string SchemaVersion = "sagajeong-synthetic-courier-observation.v1";
        public const string ScenarioStableId = "scenario:synthetic-delivery.r1";
        public const string ScenarioDataRevision = "synthetic-delivery.r1";
        public const string TransitStationStableId =
            "station:kr:kric:s1107:0722";
        public const string ActorStableId = "actor:synthetic-courier:1";
        public const string DisplayAlias = "사가정 합성 배달 기사 1";
        public const string RoleCode = "SyntheticFoodCourier";
        public const string RoleDisplayName = "합성 배달 기사";
        public const string DeliveryScopeStableId = "delivery-scope:synthetic";
        public const string CountAttributionBasisCode = "SyntheticDeliveryR1SingleCourierScope";
    }

    public static class 사가정합성기사관찰MetricStatusCodes
    {
        public const string Tracked = "Tracked";
        public const string NotTracked = "NotTracked";
    }

    public static class 사가정합성기사관찰RouteProgressStatusCodes
    {
        public const string Ready = "Ready";
        public const string NotApplicable = "NotApplicable";
    }

    public static class 사가정합성기사관찰RouteProgressBasisCodes
    {
        /// <summary>전체 도로망 진척이 아니라 현재 합성 단계 안의 거리이다.</summary>
        public const string StageLocalDistanceMeters = "StageLocalDistanceMeters";
    }

    public static class 사가정합성기사관찰SettlementStatusCodes
    {
        public const string PolicyPending = "PolicyPending";
        public const string SimulationSettlementRulePending =
            "SimulationSettlementRulePending";
    }

    public sealed class 사가정합성기사모의지급Snapshot
    {
        public string StatusCode { get; set; }
            = 사가정합성기사관찰SettlementStatusCodes.PolicyPending;
        public string BlockReasonCode { get; set; }
            = 사가정합성기사관찰SettlementStatusCodes
                .SimulationSettlementRulePending;
        public string PolicyRevision { get; set; } = string.Empty;
        public long? AmountMinor { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// synthetic-delivery.r1의 단일 합성 기사 상태를 선택 카드용으로 축약한
    /// 읽기 전용 사본이다. 공공 통계로 개인을 생성하거나 실제 기사 실적을
    /// 나타내지 않으며, 이 사본을 적용해도 Simulation 권위 상태는 바뀌지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelCodeMetadata(
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeFeatureKeys
            .SimulationWorldDerivation,
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeLayer.Contract,
        "사가정 synthetic-delivery.r1 단일 기사의 선택 카드용 읽기 사본을 정의한다.",
        StepKey = "contract.sagajeong-synthetic-courier-observation",
        FlowOrder = 138,
        ExecutionStage = Ssalddel.Contracts.Common.Metadata
            .SsalddelCodeExecutionStage.Definition,
        ReadsFrom = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope
            .DerivedWorld,
        Effects = Ssalddel.Contracts.Common.Metadata.SsalddelCodeEffect.None,
        Boundary = "합성 Simulation 관찰 전용이며 실제 사람·운영 배차·실제 정산·권위 상태 변경을 허용하지 않는다.")]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "사가정 합성 기사 카드의 정체성·업무·경로 진행·집계·모의 지급 차단 계약을 정의한다.",
        Boundary = "계약 정의는 Unity 배치·실제 입력·운영 실적·정산 증거를 소유하지 않는다.")]
    public sealed class 사가정합성기사관찰Snapshot
    {
        public string SchemaVersion { get; set; }
            = 사가정합성기사관찰Policy.SchemaVersion;
        public string SessionStableId { get; set; } = string.Empty;
        /// <summary>
        /// 현행 세션 원본에는 역 필드가 없으므로 factory가 사가정 전용 정책 ID를
        /// 명시한다. 원본에서 발견하거나 다른 역을 사가정으로 추론한 값이 아니다.
        /// </summary>
        public string TransitStationStableId { get; set; } = string.Empty;
        public string ScenarioStableId { get; set; } = string.Empty;
        public string ScenarioDataRevision { get; set; } = string.Empty;
        public long SourceSessionRevision { get; set; }
        public long SourceWorldRevision { get; set; }
        public int CurrentTick { get; set; }
        public int DurationTicks { get; set; }

        public string ActorStableId { get; set; } = string.Empty;
        public string DisplayAlias { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string RoleDisplayName { get; set; } = string.Empty;
        public string CurrentStageCode { get; set; } = string.Empty;
        public string CurrentStageDisplayName { get; set; } = string.Empty;
        public string CurrentActionCode { get; set; } = string.Empty;
        public string CurrentActionDisplayName { get; set; } = string.Empty;
        public string CurrentOrderStableId { get; set; } = string.Empty;
        public string CurrentOrderStateCode { get; set; } = string.Empty;

        public string RouteProgressBasisCode { get; set; } = string.Empty;
        public string RouteProgressStatusCode { get; set; } = string.Empty;
        public double RouteProgressMeters { get; set; }
        public double RouteLengthMeters { get; set; }
        public double? RouteProgressRatio { get; set; }
        public double ActorX { get; set; }
        public double ActorZ { get; set; }
        public double VehicleX { get; set; }
        public double VehicleZ { get; set; }
        public bool Carrying { get; set; }

        public string CountAttributionBasisCode { get; set; } = string.Empty;
        public int CompletedDeliveryCount { get; set; }
        public int InProgressDeliveryCount { get; set; }
        public int? FailedDeliveryCount { get; set; }
        public string FailureCountStatusCode { get; set; } = string.Empty;
        public int? RecoveredDeliveryCount { get; set; }
        public string RecoveryCountStatusCode { get; set; } = string.Empty;

        public 사가정합성기사모의지급Snapshot SyntheticCompensation { get; set; }
            = new 사가정합성기사모의지급Snapshot();

        public bool Synthetic { get; set; }
        public bool ActualPerson { get; set; }
        public bool Operational { get; set; }
        public bool SettlementWriteAllowed { get; set; }
        public bool ChangesAuthorityState { get; set; }
        public bool ObservationPresentationOnly { get; set; }
        public string ProjectionFingerprintSha256 { get; set; } = string.Empty;
    }

    /// <summary>
    /// 서버와 Unity가 같은 카드 내용 지문을 계산하는 wire 호환 알고리즘이다.
    /// 지문은 서명이 아니며, 신뢰한 전송·판본 검증을 대신하지 않는다.
    /// </summary>
    public static class 사가정합성기사관찰Fingerprint
    {
        public static string Compute(사가정합성기사관찰Snapshot value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                Write(writer, value.SchemaVersion);
                Write(writer, value.SessionStableId);
                Write(writer, value.TransitStationStableId);
                Write(writer, value.ScenarioStableId);
                Write(writer, value.ScenarioDataRevision);
                writer.Write(value.SourceSessionRevision);
                writer.Write(value.SourceWorldRevision);
                writer.Write(value.CurrentTick);
                writer.Write(value.DurationTicks);
                Write(writer, value.ActorStableId);
                Write(writer, value.DisplayAlias);
                Write(writer, value.RoleCode);
                Write(writer, value.RoleDisplayName);
                Write(writer, value.CurrentStageCode);
                Write(writer, value.CurrentStageDisplayName);
                Write(writer, value.CurrentActionCode);
                Write(writer, value.CurrentActionDisplayName);
                Write(writer, value.CurrentOrderStableId);
                Write(writer, value.CurrentOrderStateCode);
                Write(writer, value.RouteProgressBasisCode);
                Write(writer, value.RouteProgressStatusCode);
                writer.Write(value.RouteProgressMeters);
                writer.Write(value.RouteLengthMeters);
                writer.Write(value.RouteProgressRatio.HasValue);
                if (value.RouteProgressRatio.HasValue)
                    writer.Write(value.RouteProgressRatio.Value);
                writer.Write(value.ActorX);
                writer.Write(value.ActorZ);
                writer.Write(value.VehicleX);
                writer.Write(value.VehicleZ);
                writer.Write(value.Carrying);
                Write(writer, value.CountAttributionBasisCode);
                writer.Write(value.CompletedDeliveryCount);
                writer.Write(value.InProgressDeliveryCount);
                writer.Write(value.FailedDeliveryCount.HasValue);
                if (value.FailedDeliveryCount.HasValue)
                    writer.Write(value.FailedDeliveryCount.Value);
                Write(writer, value.FailureCountStatusCode);
                writer.Write(value.RecoveredDeliveryCount.HasValue);
                if (value.RecoveredDeliveryCount.HasValue)
                    writer.Write(value.RecoveredDeliveryCount.Value);
                Write(writer, value.RecoveryCountStatusCode);
                var compensation = value.SyntheticCompensation;
                writer.Write(compensation != null);
                if (compensation != null)
                {
                    Write(writer, compensation.StatusCode);
                    Write(writer, compensation.BlockReasonCode);
                    Write(writer, compensation.PolicyRevision);
                    writer.Write(compensation.AmountMinor.HasValue);
                    if (compensation.AmountMinor.HasValue)
                        writer.Write(compensation.AmountMinor.Value);
                    Write(writer, compensation.CurrencyCode);
                }
                writer.Write(value.Synthetic);
                writer.Write(value.ActualPerson);
                writer.Write(value.Operational);
                writer.Write(value.SettlementWriteAllowed);
                writer.Write(value.ChangesAuthorityState);
                writer.Write(value.ObservationPresentationOnly);
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray()))
                .Replace("-", string.Empty);
        }

        private static void Write(BinaryWriter writer, string value)
            => writer.Write(value ?? string.Empty);
    }
}
