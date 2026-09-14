using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Application
{
    public static class 사가정합성기사관찰ProjectionErrorCodes
    {
        public const string InputInvalid = "SagajeongSyntheticCourierObservationInputInvalid";
        public const string ScenarioUnsupported = "SagajeongSyntheticCourierObservationScenarioUnsupported";
        public const string OperationalSourceForbidden = "SagajeongSyntheticCourierObservationOperationalSourceForbidden";
        public const string SingleCourierRequired = "SagajeongSyntheticCourierObservationSingleCourierRequired";
        public const string ActorMismatch = "SagajeongSyntheticCourierObservationActorMismatch";
        public const string CurrentOrderMismatch = "SagajeongSyntheticCourierObservationCurrentOrderMismatch";
        public const string SnapshotShapeInvalid = "SagajeongSyntheticCourierObservationSnapshotShapeInvalid";
        public const string FingerprintMismatch = "SagajeongSyntheticCourierObservationFingerprintMismatch";
        public const string IdentityMismatch = "SagajeongSyntheticCourierObservationIdentityMismatch";
        public const string StaleRevision = "SagajeongSyntheticCourierObservationStaleRevision";
        public const string StaleTick = "SagajeongSyntheticCourierObservationStaleTick";
        public const string RevisionConflict = "SagajeongSyntheticCourierObservationRevisionConflict";
    }

    /// <summary>
    /// 권위 세션 사본에서 synthetic-delivery.r1 단일 기사 선택 카드 사본을 만든다.
    /// 완료 건수는 r1의 단일 기사 귀속 조건 아래 수령확인과 ReceivedTick이 모두
    /// 존재하는 합성 배달만 센다. 실패·회복과 지급액은 근거가 없어 추정하지 않는다.
    /// 현행 세션 사본에는 역 식별자가 없으므로 이 factory는 범용 역 탐지기가 아니라
    /// 사가정 전용 Adapter 경계이며, 사가정 정책 ID를 명시적으로 기록한다.
    /// </summary>
    [SsalddelCodeMetadata(
        SsalddelCodeFeatureKeys.SimulationWorldDerivation,
        SsalddelCodeLayer.Application,
        "synthetic-delivery.r1 단일 기사 상태를 사가정 선택 카드용 사본으로 투영한다.",
        StepKey = "application.sagajeong-synthetic-courier-observation-projection",
        DependsOnStepKeys = new[]
        {
            "contract.sagajeong-synthetic-courier-observation"
        },
        FlowOrder = 139,
        ExecutionStage = SsalddelCodeExecutionStage.Projection,
        ReadsFrom = SsalddelCodeDataScope.DerivedWorld,
        Effects = SsalddelCodeEffect.None,
        Boundary = "Simulation 세션 사본만 읽으며 공공 통계·운영 주문·실제 기사·실제 정산·Unity 상태를 변경하지 않는다.")]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "사가정 합성 기사 한 명의 현재 업무·단계 로컬 경로 진행·수령 완료 건수를 결정적으로 투영한다.",
        Boundary = "실패·회복 건수와 지급액은 미추적 또는 정책 미정으로 보존하며 운영 효과를 만들지 않는다.")]
    public sealed class 사가정합성기사관찰SnapshotFactory
    {
        public 사가정합성기사관찰Snapshot 생성(
            경영SimulationSessionSnapshot source,
            long minimumRevision = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            요구(minimumRevision > 0,
                사가정합성기사관찰ProjectionErrorCodes.InputInvalid);
            요구(string.Equals(source.ScenarioStableId,
                    사가정합성기사관찰Policy.ScenarioStableId,
                    StringComparison.Ordinal)
                && string.Equals(source.ScenarioDataRevision,
                    사가정합성기사관찰Policy.ScenarioDataRevision,
                    StringComparison.Ordinal),
                사가정합성기사관찰ProjectionErrorCodes.ScenarioUnsupported);
            요구(!source.IsOperationalState
                && string.Equals(source.ModeCode, SimulationModeCodes.Simulation,
                    StringComparison.Ordinal),
                사가정합성기사관찰ProjectionErrorCodes
                    .OperationalSourceForbidden);
            요구(source.WaitingFleet == null && source.SyntheticCourier != null,
                사가정합성기사관찰ProjectionErrorCodes
                    .SingleCourierRequired);
            요구(source.Revision >= minimumRevision && source.Revision > 0,
                사가정합성기사관찰ProjectionErrorCodes.StaleRevision);
            var worldContext = source.WorldContext;
            요구(!string.IsNullOrWhiteSpace(source.SessionStableId)
                && source.CurrentTick >= 0 && source.DurationTicks > 0
                && source.CurrentTick <= source.DurationTicks
                && worldContext != null
                && worldContext.WorldTick == source.CurrentTick
                && worldContext.WorldRevision == source.Revision,
                사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            if (worldContext == null)
                throw new InvalidDataException(
                    사가정합성기사관찰ProjectionErrorCodes
                        .SnapshotShapeInvalid);

            var courier = source.SyntheticCourier!;
            요구(string.Equals(courier.ActorStableId,
                    사가정합성기사관찰Policy.ActorStableId,
                    StringComparison.Ordinal),
                사가정합성기사관찰ProjectionErrorCodes.ActorMismatch);
            var stage = 사가정합성기사관찰StageCatalog.조회(courier.Stage);
            요구(courier.Batch >= 0 && 유한(courier.Distance)
                && 유한(courier.X) && 유한(courier.Z)
                && 유한(courier.VehicleX) && 유한(courier.VehicleZ)
                && courier.Distance >= 0
                && courier.Distance <= stage.RouteLengthMeters,
                사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            if (!stage.HasRoute)
                요구(courier.Distance == 0,
                    사가정합성기사관찰ProjectionErrorCodes
                        .SnapshotShapeInvalid);

            var orders = source.FoodDeliveries;
            요구(orders != null,
                사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            var syntheticOrders = orders
                .Where(value => value != null
                    && string.Equals(value.DeliveryScopeStableId,
                        사가정합성기사관찰Policy.DeliveryScopeStableId,
                        StringComparison.Ordinal))
                .ToArray();
            요구(orders.All(value => value != null),
                사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            합성주문형태요구(syntheticOrders, source.CurrentTick);

            Simulation음식배달Snapshot? currentOrder = null;
            if (string.Equals(courier.Stage, "Idle", StringComparison.Ordinal))
            {
                요구(string.IsNullOrEmpty(courier.OrderStableId),
                    사가정합성기사관찰ProjectionErrorCodes
                        .CurrentOrderMismatch);
            }
            else
            {
                요구(합성Id(courier.OrderStableId),
                    사가정합성기사관찰ProjectionErrorCodes
                        .CurrentOrderMismatch);
                var matches = syntheticOrders.Where(value => string.Equals(
                    value.FoodOrderStableId, courier.OrderStableId,
                    StringComparison.Ordinal)).ToArray();
                요구(matches.Length == 1,
                    사가정합성기사관찰ProjectionErrorCodes
                        .CurrentOrderMismatch);
                currentOrder = matches[0];
            }

            var completed = syntheticOrders.Count(value => string.Equals(
                    value.StateCode, 음식배달상태코드.수령확인,
                    StringComparison.Ordinal)
                && value.ReceivedTick.HasValue);
            var inProgress = currentOrder != null
                && !종료상태(currentOrder.StateCode) ? 1 : 0;
            var routeRatio = stage.HasRoute
                ? courier.Distance / stage.RouteLengthMeters
                : (double?)null;
            var snapshot = new 사가정합성기사관찰Snapshot
            {
                SessionStableId = source.SessionStableId,
                TransitStationStableId = 사가정합성기사관찰Policy
                    .TransitStationStableId,
                ScenarioStableId = source.ScenarioStableId,
                ScenarioDataRevision = source.ScenarioDataRevision,
                SourceSessionRevision = source.Revision,
                SourceWorldRevision = worldContext.WorldRevision,
                CurrentTick = source.CurrentTick,
                DurationTicks = source.DurationTicks,
                ActorStableId = courier.ActorStableId,
                DisplayAlias = 사가정합성기사관찰Policy.DisplayAlias,
                RoleCode = 사가정합성기사관찰Policy.RoleCode,
                RoleDisplayName = 사가정합성기사관찰Policy.RoleDisplayName,
                CurrentStageCode = courier.Stage,
                CurrentStageDisplayName = stage.StageDisplayName,
                CurrentActionCode = stage.ActionCode,
                CurrentActionDisplayName = stage.ActionDisplayName,
                CurrentOrderStableId = currentOrder?.FoodOrderStableId
                    ?? string.Empty,
                CurrentOrderStateCode = currentOrder?.StateCode
                    ?? string.Empty,
                RouteProgressBasisCode = 사가정합성기사관찰RouteProgressBasisCodes
                    .StageLocalDistanceMeters,
                RouteProgressStatusCode = stage.HasRoute
                    ? 사가정합성기사관찰RouteProgressStatusCodes.Ready
                    : 사가정합성기사관찰RouteProgressStatusCodes.NotApplicable,
                RouteProgressMeters = courier.Distance,
                RouteLengthMeters = stage.RouteLengthMeters,
                RouteProgressRatio = routeRatio,
                ActorX = courier.X,
                ActorZ = courier.Z,
                VehicleX = courier.VehicleX,
                VehicleZ = courier.VehicleZ,
                Carrying = courier.Carrying,
                CountAttributionBasisCode = 사가정합성기사관찰Policy
                    .CountAttributionBasisCode,
                CompletedDeliveryCount = completed,
                InProgressDeliveryCount = inProgress,
                FailedDeliveryCount = null,
                FailureCountStatusCode = 사가정합성기사관찰MetricStatusCodes
                    .NotTracked,
                RecoveredDeliveryCount = null,
                RecoveryCountStatusCode = 사가정합성기사관찰MetricStatusCodes
                    .NotTracked,
                SyntheticCompensation = new 사가정합성기사모의지급Snapshot
                {
                    StatusCode = 사가정합성기사관찰SettlementStatusCodes
                        .PolicyPending,
                    BlockReasonCode = 사가정합성기사관찰SettlementStatusCodes
                        .SimulationSettlementRulePending,
                    PolicyRevision = string.Empty,
                    AmountMinor = null,
                    CurrencyCode = string.Empty
                },
                Synthetic = true,
                ActualPerson = false,
                Operational = false,
                SettlementWriteAllowed = false,
                ChangesAuthorityState = false,
                ObservationPresentationOnly = true
            };
            snapshot.ProjectionFingerprintSha256 =
                사가정합성기사관찰Fingerprint.Compute(snapshot);
            return snapshot;
        }

        private static void 합성주문형태요구(
            IReadOnlyCollection<Simulation음식배달Snapshot> orders,
            int currentTick)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var order in orders)
            {
                요구(합성Id(order.FoodOrderStableId)
                    && ids.Add(order.FoodOrderStableId)
                    && order.Revision > 0
                    && 지원상태(order.StateCode)
                    && order.AcceptedTick >= 0
                    && order.AcceptedTick <= currentTick,
                    사가정합성기사관찰ProjectionErrorCodes
                        .SnapshotShapeInvalid);
                var previous = order.AcceptedTick;
                foreach (var tick in new int?[]
                {
                    order.CookingStartedTick,
                    order.ReadyForPickupTick,
                    order.DispatchCandidateTick,
                    order.PickedUpTick,
                    order.DeliveredTick,
                    order.ReceivedTick
                })
                {
                    if (!tick.HasValue) continue;
                    요구(tick.Value >= previous && tick.Value <= currentTick,
                        사가정합성기사관찰ProjectionErrorCodes
                            .SnapshotShapeInvalid);
                    previous = tick.Value;
                }
                요구(order.ReceivedTick.HasValue == string.Equals(
                        order.StateCode, 음식배달상태코드.수령확인,
                        StringComparison.Ordinal),
                    사가정합성기사관찰ProjectionErrorCodes
                        .SnapshotShapeInvalid);
            }
        }

        private static bool 지원상태(string value)
            => new[]
            {
                음식배달상태코드.주문대기,
                음식배달상태코드.조리중,
                음식배달상태코드.픽업대기,
                음식배달상태코드.기사배정,
                음식배달상태코드.픽업완료,
                음식배달상태코드.전달완료,
                음식배달상태코드.수령확인,
                음식배달상태코드.거절,
                음식배달상태코드.취소
            }.Contains(value, StringComparer.Ordinal);

        private static bool 종료상태(string value)
            => string.Equals(value, 음식배달상태코드.수령확인,
                    StringComparison.Ordinal)
                || string.Equals(value, 음식배달상태코드.거절,
                    StringComparison.Ordinal)
                || string.Equals(value, 음식배달상태코드.취소,
                    StringComparison.Ordinal);

        private static bool 합성Id(string value)
            => !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(":synthetic:", StringComparison.Ordinal) >= 0;

        private static bool 유한(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void 요구(bool condition, string errorCode)
        {
            if (!condition) throw new InvalidDataException(errorCode);
        }
    }

    public sealed class 사가정합성기사관찰ValidationResult
    {
        public bool Accepted { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
    }

    /// <summary>wire 카드 사본의 고정 경계와 결정적 지문을 검증한다.</summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "사가정 합성 기사 카드 사본의 scenario·actor·통계 미추적·지급 차단·지문을 검증한다.",
        Boundary = "검증 성공은 Unity 실제 클릭·Game View·운영 기사 또는 정산의 증거가 아니다.")]
    public sealed class 사가정합성기사관찰SnapshotValidator
    {
        public 사가정합성기사관찰ValidationResult 검증(
            사가정합성기사관찰Snapshot snapshot)
        {
            if (snapshot == null)
                return 거절(사가정합성기사관찰ProjectionErrorCodes.InputInvalid);
            if (!string.Equals(snapshot.SchemaVersion,
                    사가정합성기사관찰Policy.SchemaVersion,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(snapshot.SessionStableId)
                || !string.Equals(snapshot.TransitStationStableId,
                    사가정합성기사관찰Policy.TransitStationStableId,
                    StringComparison.Ordinal)
                || !string.Equals(snapshot.ScenarioStableId,
                    사가정합성기사관찰Policy.ScenarioStableId,
                    StringComparison.Ordinal)
                || !string.Equals(snapshot.ScenarioDataRevision,
                    사가정합성기사관찰Policy.ScenarioDataRevision,
                    StringComparison.Ordinal)
                || snapshot.SourceSessionRevision <= 0
                || snapshot.SourceWorldRevision != snapshot.SourceSessionRevision
                || snapshot.CurrentTick < 0 || snapshot.DurationTicks <= 0
                || snapshot.CurrentTick > snapshot.DurationTicks)
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            if (!string.Equals(snapshot.ActorStableId,
                    사가정합성기사관찰Policy.ActorStableId,
                    StringComparison.Ordinal))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .ActorMismatch);
            if (!고정표시경계(snapshot) || !집계와지급경계(snapshot)
                || !위치와진행형태(snapshot))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .SnapshotShapeInvalid);
            if (!해시형식(snapshot.ProjectionFingerprintSha256)
                || !string.Equals(snapshot.ProjectionFingerprintSha256,
                    사가정합성기사관찰Fingerprint.Compute(snapshot),
                    StringComparison.Ordinal))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .FingerprintMismatch);
            return new 사가정합성기사관찰ValidationResult
            { Accepted = true };
        }

        private static bool 고정표시경계(사가정합성기사관찰Snapshot value)
        {
            if (!string.Equals(value.DisplayAlias,
                    사가정합성기사관찰Policy.DisplayAlias,
                    StringComparison.Ordinal)
                || !string.Equals(value.RoleCode,
                    사가정합성기사관찰Policy.RoleCode,
                    StringComparison.Ordinal)
                || !string.Equals(value.RoleDisplayName,
                    사가정합성기사관찰Policy.RoleDisplayName,
                    StringComparison.Ordinal)
                || !value.Synthetic || value.ActualPerson || value.Operational
                || value.SettlementWriteAllowed || value.ChangesAuthorityState
                || !value.ObservationPresentationOnly)
                return false;
            if (!사가정합성기사관찰StageCatalog.Try조회(
                    value.CurrentStageCode, out var stage)
                || !string.Equals(value.CurrentStageDisplayName,
                    stage.StageDisplayName, StringComparison.Ordinal)
                || !string.Equals(value.CurrentActionCode, stage.ActionCode,
                    StringComparison.Ordinal)
                || !string.Equals(value.CurrentActionDisplayName,
                    stage.ActionDisplayName, StringComparison.Ordinal))
                return false;
            if (string.Equals(value.CurrentStageCode, "Idle",
                    StringComparison.Ordinal))
                return string.IsNullOrEmpty(value.CurrentOrderStableId)
                    && string.IsNullOrEmpty(value.CurrentOrderStateCode);
            return 합성Id(value.CurrentOrderStableId)
                && 지원상태(value.CurrentOrderStateCode);
        }

        private static bool 집계와지급경계(사가정합성기사관찰Snapshot value)
        {
            var compensation = value.SyntheticCompensation;
            return string.Equals(value.CountAttributionBasisCode,
                    사가정합성기사관찰Policy.CountAttributionBasisCode,
                    StringComparison.Ordinal)
                && value.CompletedDeliveryCount >= 0
                && value.InProgressDeliveryCount >= 0
                && value.InProgressDeliveryCount <= 1
                && !value.FailedDeliveryCount.HasValue
                && string.Equals(value.FailureCountStatusCode,
                    사가정합성기사관찰MetricStatusCodes.NotTracked,
                    StringComparison.Ordinal)
                && !value.RecoveredDeliveryCount.HasValue
                && string.Equals(value.RecoveryCountStatusCode,
                    사가정합성기사관찰MetricStatusCodes.NotTracked,
                    StringComparison.Ordinal)
                && compensation != null
                && string.Equals(compensation.StatusCode,
                    사가정합성기사관찰SettlementStatusCodes.PolicyPending,
                    StringComparison.Ordinal)
                && string.Equals(compensation.BlockReasonCode,
                    사가정합성기사관찰SettlementStatusCodes
                        .SimulationSettlementRulePending,
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(compensation.PolicyRevision)
                && !compensation.AmountMinor.HasValue
                && string.IsNullOrEmpty(compensation.CurrencyCode);
        }

        private static bool 위치와진행형태(사가정합성기사관찰Snapshot value)
        {
            if (!유한(value.ActorX) || !유한(value.ActorZ)
                || !유한(value.VehicleX) || !유한(value.VehicleZ)
                || !유한(value.RouteProgressMeters)
                || !유한(value.RouteLengthMeters)
                || value.RouteProgressMeters < 0
                || !string.Equals(value.RouteProgressBasisCode,
                    사가정합성기사관찰RouteProgressBasisCodes
                        .StageLocalDistanceMeters,
                    StringComparison.Ordinal)
                || !사가정합성기사관찰StageCatalog.Try조회(
                    value.CurrentStageCode, out var stage)
                || Math.Abs(value.RouteLengthMeters
                    - stage.RouteLengthMeters) > 0.000001)
                return false;
            if (!stage.HasRoute)
                return value.RouteProgressMeters == 0
                    && value.RouteLengthMeters == 0
                    && !value.RouteProgressRatio.HasValue
                    && string.Equals(value.RouteProgressStatusCode,
                        사가정합성기사관찰RouteProgressStatusCodes
                            .NotApplicable,
                        StringComparison.Ordinal);
            if (!value.RouteProgressRatio.HasValue
                || !유한(value.RouteProgressRatio.Value)
                || value.RouteProgressMeters > value.RouteLengthMeters)
                return false;
            return string.Equals(value.RouteProgressStatusCode,
                    사가정합성기사관찰RouteProgressStatusCodes.Ready,
                    StringComparison.Ordinal)
                && Math.Abs(value.RouteProgressRatio.Value
                    - value.RouteProgressMeters / value.RouteLengthMeters)
                    <= 0.000001;
        }

        private static bool 지원상태(string value)
            => new[]
            {
                음식배달상태코드.주문대기,
                음식배달상태코드.조리중,
                음식배달상태코드.픽업대기,
                음식배달상태코드.기사배정,
                음식배달상태코드.픽업완료,
                음식배달상태코드.전달완료,
                음식배달상태코드.수령확인,
                음식배달상태코드.거절,
                음식배달상태코드.취소
            }.Contains(value, StringComparer.Ordinal);

        private static bool 합성Id(string value)
            => !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(":synthetic:", StringComparison.Ordinal) >= 0;

        private static bool 해시형식(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character >= '0' && character <= '9'
                || character >= 'A' && character <= 'F');

        private static bool 유한(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static 사가정합성기사관찰ValidationResult 거절(string code)
            => new 사가정합성기사관찰ValidationResult { ErrorCode = code };
    }

    public sealed class 사가정합성기사관찰ApplyResult
    {
        public bool Accepted { get; internal set; }
        public bool Changed { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
        public 사가정합성기사관찰Snapshot? Snapshot { get; internal set; }
    }

    /// <summary>
    /// 검증된 최신 카드 사본만 보존한다. 운영 저장소·Command·정산 쓰기 경로를
    /// 갖지 않으며, 소비자가 받은 객체를 변경해도 내부 사본은 변하지 않는다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "검증된 최신 사가정 합성 기사 카드를 revision 충돌 규칙으로 메모리에 투영한다.",
        Boundary = "표시 온·오프와 메모리 적용은 Simulation 세션 revision·업무 상태·정산을 변경하지 않는다.")]
    public sealed class 사가정합성기사관찰상태Projector
    {
        private readonly 사가정합성기사관찰SnapshotValidator validator =
            new 사가정합성기사관찰SnapshotValidator();
        private 사가정합성기사관찰Snapshot? current;

        public 사가정합성기사관찰Snapshot? Current
            => current == null ? null : Copy(current);

        public 사가정합성기사관찰ApplyResult 적용(
            사가정합성기사관찰Snapshot incoming,
            string expectedSessionStableId,
            string expectedActorStableId)
            => 적용(incoming, expectedSessionStableId,
                사가정합성기사관찰Policy.TransitStationStableId,
                expectedActorStableId);

        public 사가정합성기사관찰ApplyResult 적용(
            사가정합성기사관찰Snapshot incoming,
            string expectedSessionStableId,
            string expectedTransitStationStableId,
            string expectedActorStableId)
        {
            if (string.IsNullOrWhiteSpace(expectedSessionStableId)
                || string.IsNullOrWhiteSpace(expectedTransitStationStableId)
                || string.IsNullOrWhiteSpace(expectedActorStableId))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .InputInvalid);
            var validation = validator.검증(incoming);
            if (!validation.Accepted) return 거절(validation.ErrorCode);
            if (!string.Equals(incoming.SessionStableId,
                    expectedSessionStableId, StringComparison.Ordinal))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .IdentityMismatch);
            if (!string.Equals(incoming.TransitStationStableId,
                    expectedTransitStationStableId, StringComparison.Ordinal))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .IdentityMismatch);
            if (!string.Equals(incoming.ActorStableId,
                    expectedActorStableId, StringComparison.Ordinal))
                return 거절(사가정합성기사관찰ProjectionErrorCodes
                    .ActorMismatch);

            if (current != null)
            {
                if (!string.Equals(current.SessionStableId,
                        incoming.SessionStableId, StringComparison.Ordinal)
                    || !string.Equals(current.TransitStationStableId,
                        incoming.TransitStationStableId,
                        StringComparison.Ordinal)
                    || !string.Equals(current.ActorStableId,
                        incoming.ActorStableId, StringComparison.Ordinal)
                    || !string.Equals(current.ScenarioStableId,
                        incoming.ScenarioStableId, StringComparison.Ordinal)
                    || !string.Equals(current.ScenarioDataRevision,
                        incoming.ScenarioDataRevision,
                        StringComparison.Ordinal))
                    return 거절(사가정합성기사관찰ProjectionErrorCodes
                        .IdentityMismatch);
                if (incoming.SourceSessionRevision
                    < current.SourceSessionRevision)
                    return 거절(사가정합성기사관찰ProjectionErrorCodes
                        .StaleRevision);
                if (incoming.SourceSessionRevision
                    == current.SourceSessionRevision)
                {
                    if (!string.Equals(incoming.ProjectionFingerprintSha256,
                            current.ProjectionFingerprintSha256,
                            StringComparison.Ordinal))
                        return 거절(사가정합성기사관찰ProjectionErrorCodes
                            .RevisionConflict);
                    return new 사가정합성기사관찰ApplyResult
                    {
                        Accepted = true,
                        Changed = false,
                        Snapshot = Copy(current)
                    };
                }
                if (incoming.CurrentTick < current.CurrentTick)
                    return 거절(사가정합성기사관찰ProjectionErrorCodes
                        .StaleTick);
            }
            current = Copy(incoming);
            return new 사가정합성기사관찰ApplyResult
            {
                Accepted = true,
                Changed = true,
                Snapshot = Copy(current)
            };
        }

        public void Clear() => current = null;

        private 사가정합성기사관찰ApplyResult 거절(string code)
            => new 사가정합성기사관찰ApplyResult
            {
                ErrorCode = code,
                Snapshot = current == null ? null : Copy(current)
            };

        private static 사가정합성기사관찰Snapshot Copy(
            사가정합성기사관찰Snapshot value)
            => new 사가정합성기사관찰Snapshot
            {
                SchemaVersion = value.SchemaVersion,
                SessionStableId = value.SessionStableId,
                TransitStationStableId = value.TransitStationStableId,
                ScenarioStableId = value.ScenarioStableId,
                ScenarioDataRevision = value.ScenarioDataRevision,
                SourceSessionRevision = value.SourceSessionRevision,
                SourceWorldRevision = value.SourceWorldRevision,
                CurrentTick = value.CurrentTick,
                DurationTicks = value.DurationTicks,
                ActorStableId = value.ActorStableId,
                DisplayAlias = value.DisplayAlias,
                RoleCode = value.RoleCode,
                RoleDisplayName = value.RoleDisplayName,
                CurrentStageCode = value.CurrentStageCode,
                CurrentStageDisplayName = value.CurrentStageDisplayName,
                CurrentActionCode = value.CurrentActionCode,
                CurrentActionDisplayName = value.CurrentActionDisplayName,
                CurrentOrderStableId = value.CurrentOrderStableId,
                CurrentOrderStateCode = value.CurrentOrderStateCode,
                RouteProgressBasisCode = value.RouteProgressBasisCode,
                RouteProgressStatusCode = value.RouteProgressStatusCode,
                RouteProgressMeters = value.RouteProgressMeters,
                RouteLengthMeters = value.RouteLengthMeters,
                RouteProgressRatio = value.RouteProgressRatio,
                ActorX = value.ActorX,
                ActorZ = value.ActorZ,
                VehicleX = value.VehicleX,
                VehicleZ = value.VehicleZ,
                Carrying = value.Carrying,
                CountAttributionBasisCode = value.CountAttributionBasisCode,
                CompletedDeliveryCount = value.CompletedDeliveryCount,
                InProgressDeliveryCount = value.InProgressDeliveryCount,
                FailedDeliveryCount = value.FailedDeliveryCount,
                FailureCountStatusCode = value.FailureCountStatusCode,
                RecoveredDeliveryCount = value.RecoveredDeliveryCount,
                RecoveryCountStatusCode = value.RecoveryCountStatusCode,
                SyntheticCompensation = new 사가정합성기사모의지급Snapshot
                {
                    StatusCode = value.SyntheticCompensation.StatusCode,
                    BlockReasonCode = value.SyntheticCompensation
                        .BlockReasonCode,
                    PolicyRevision = value.SyntheticCompensation
                        .PolicyRevision,
                    AmountMinor = value.SyntheticCompensation.AmountMinor,
                    CurrencyCode = value.SyntheticCompensation.CurrencyCode
                },
                Synthetic = value.Synthetic,
                ActualPerson = value.ActualPerson,
                Operational = value.Operational,
                SettlementWriteAllowed = value.SettlementWriteAllowed,
                ChangesAuthorityState = value.ChangesAuthorityState,
                ObservationPresentationOnly = value
                    .ObservationPresentationOnly,
                ProjectionFingerprintSha256 = value
                    .ProjectionFingerprintSha256
            };
    }

    internal static class 사가정합성기사관찰StageCatalog
    {
        private static readonly IReadOnlyDictionary<string, StageInfo> Stages =
            new Dictionary<string, StageInfo>(StringComparer.Ordinal)
            {
                ["Idle"] = new StageInfo("업무 대기",
                    "WaitForAssignment", "다음 합성 주문 대기", 0),
                ["DriveRestaurant"] = new StageInfo("픽업 장소로 운전",
                    "MoveToPickupByVehicle", "픽업 장소로 이동", 10),
                ["WalkRestaurant"] = new StageInfo("픽업점 접근",
                    "WalkToPickup", "픽업점 출입구로 이동", 4),
                ["WaitFood"] = new StageInfo("조리 완료 대기",
                    "WaitForPreparation", "조리 완료 대기", 0),
                ["Pickup"] = new StageInfo("물품 픽업",
                    "PickupOrder", "주문 물품 픽업", 0),
                ["WalkRestaurantOut"] = new StageInfo("차량으로 복귀",
                    "ReturnToVehicle", "픽업점에서 차량으로 복귀", 4),
                ["DriveHome"] = new StageInfo("주문지로 운전",
                    "MoveToDeliveryDestination", "주문지로 이동", 40),
                ["WalkHome"] = new StageInfo("주택 출입구 접근",
                    "WalkToDeliveryEntrance", "전달 지점으로 도보 이동", 4),
                ["Deliver"] = new StageInfo("주문자에게 전달",
                    "HandOffOrder", "주문 물품 전달", 0),
                ["Receive"] = new StageInfo("수령 확인 대기",
                    "WaitForReceipt", "주문자 수령 확인 대기", 0),
                ["WalkHomeOut"] = new StageInfo("차량으로 복귀",
                    "ReturnToVehicle", "전달 지점에서 차량으로 복귀", 4),
                ["Return"] = new StageInfo("대기점 복귀",
                    "ReturnToStandby", "대기점으로 복귀", 50)
            };

        public static StageInfo 조회(string code)
        {
            if (!Try조회(code, out var value))
                throw new InvalidDataException(
                    사가정합성기사관찰ProjectionErrorCodes
                        .SnapshotShapeInvalid);
            return value;
        }

        public static bool Try조회(string code, out StageInfo value)
            => Stages.TryGetValue(code ?? string.Empty, out value);

        internal readonly struct StageInfo
        {
            public string StageDisplayName { get; }
            public string ActionCode { get; }
            public string ActionDisplayName { get; }
            public double RouteLengthMeters { get; }
            public bool HasRoute => RouteLengthMeters > 0;

            public StageInfo(string stageDisplayName, string actionCode,
                string actionDisplayName, double routeLengthMeters)
            {
                StageDisplayName = stageDisplayName;
                ActionCode = actionCode;
                ActionDisplayName = actionDisplayName;
                RouteLengthMeters = routeLengthMeters;
            }
        }
    }
}
