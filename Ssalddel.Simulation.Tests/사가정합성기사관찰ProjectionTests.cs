using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "사가정 synthetic-delivery.r1 단일 기사 카드의 결정성·집계·미추적·지급 차단·revision 거절을 검증한다.",
    Boundary = "단위 시험이며 Unity 실제 클릭·Game View·실제 기사 실적·운영 정산 증거가 아니다.")]
public sealed class 사가정합성기사관찰ProjectionTests
{
    [Fact]
    public void 실제r1Aggregate사본을_권위변경없이투영한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(
            가상배달관찰표본.Create(Guid.Parse(
                "669350ab-f34c-40a6-8970-a486b858615f")));
        var state = aggregate.Snapshot();
        for (var tick = 0; tick < 120; tick++)
        {
            state = aggregate.Advance(new 경영SimulationTick진행Request
            {
                CommandId = "sagajeong-card:tick:" + tick,
                ExpectedRevision = state.Revision,
                TickCount = 1
            });
            if (string.Equals(state.SyntheticCourier?.Stage, "DriveHome",
                    StringComparison.Ordinal))
                break;
        }
        Assert.Equal("DriveHome", state.SyntheticCourier?.Stage);
        var before = JsonSerializer.Serialize(state);

        var card = new 사가정합성기사관찰SnapshotFactory().생성(state);

        Assert.Equal(state.Revision, card.SourceSessionRevision);
        Assert.Equal(state.CurrentTick, card.CurrentTick);
        Assert.Equal(state.SyntheticCourier!.OrderStableId,
            card.CurrentOrderStableId);
        Assert.Equal(1, card.InProgressDeliveryCount);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void 단일합성기사를_현재업무와정확한완료건수로_결정적투영한다()
    {
        var source = 세션();
        var before = JsonSerializer.Serialize(source);
        var factory = new 사가정합성기사관찰SnapshotFactory();

        var first = factory.생성(source, 22);
        var second = factory.생성(source, 22);

        Assert.Equal(사가정합성기사관찰Policy.SchemaVersion,
            first.SchemaVersion);
        Assert.Equal(source.SessionStableId, first.SessionStableId);
        Assert.Equal("station:kr:kric:s1107:0722",
            first.TransitStationStableId);
        Assert.Equal(22, first.SourceSessionRevision);
        Assert.Equal(75, first.CurrentTick);
        Assert.Equal(사가정합성기사관찰Policy.ActorStableId,
            first.ActorStableId);
        Assert.Equal("사가정 합성 배달 기사 1", first.DisplayAlias);
        Assert.Equal("합성 배달 기사", first.RoleDisplayName);
        Assert.Equal("DriveHome", first.CurrentStageCode);
        Assert.Equal("주문지로 운전", first.CurrentStageDisplayName);
        Assert.Equal("MoveToDeliveryDestination", first.CurrentActionCode);
        Assert.Equal("주문지로 이동", first.CurrentActionDisplayName);
        Assert.Equal("food-order:synthetic:2:a",
            first.CurrentOrderStableId);
        Assert.Equal(음식배달상태코드.픽업완료,
            first.CurrentOrderStateCode);
        Assert.Equal(사가정합성기사관찰RouteProgressBasisCodes
            .StageLocalDistanceMeters, first.RouteProgressBasisCode);
        Assert.Equal(사가정합성기사관찰RouteProgressStatusCodes.Ready,
            first.RouteProgressStatusCode);
        Assert.Equal(15, first.RouteProgressMeters);
        Assert.Equal(40, first.RouteLengthMeters);
        Assert.Equal(0.375, first.RouteProgressRatio);
        Assert.Equal(1, first.CompletedDeliveryCount);
        Assert.Equal(1, first.InProgressDeliveryCount);
        Assert.Equal(사가정합성기사관찰Policy.CountAttributionBasisCode,
            first.CountAttributionBasisCode);
        Assert.True(first.Carrying);
        Assert.Matches("^[0-9A-F]{64}$", first.ProjectionFingerprintSha256);
        Assert.Equal(first.ProjectionFingerprintSha256,
            second.ProjectionFingerprintSha256);
        Assert.Equal(JsonSerializer.Serialize(first),
            JsonSerializer.Serialize(second));
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void 실패회복과모의지급은_근거없는0이나금액을만들지않는다()
    {
        var snapshot = new 사가정합성기사관찰SnapshotFactory()
            .생성(세션());

        Assert.Null(snapshot.FailedDeliveryCount);
        Assert.Equal(사가정합성기사관찰MetricStatusCodes.NotTracked,
            snapshot.FailureCountStatusCode);
        Assert.Null(snapshot.RecoveredDeliveryCount);
        Assert.Equal(사가정합성기사관찰MetricStatusCodes.NotTracked,
            snapshot.RecoveryCountStatusCode);
        Assert.Equal(사가정합성기사관찰SettlementStatusCodes.PolicyPending,
            snapshot.SyntheticCompensation.StatusCode);
        Assert.Equal(사가정합성기사관찰SettlementStatusCodes
                .SimulationSettlementRulePending,
            snapshot.SyntheticCompensation.BlockReasonCode);
        Assert.Null(snapshot.SyntheticCompensation.AmountMinor);
        Assert.Empty(snapshot.SyntheticCompensation.CurrencyCode);
        Assert.Empty(snapshot.SyntheticCompensation.PolicyRevision);
        Assert.True(snapshot.Synthetic);
        Assert.False(snapshot.ActualPerson);
        Assert.False(snapshot.Operational);
        Assert.False(snapshot.SettlementWriteAllowed);
        Assert.False(snapshot.ChangesAuthorityState);
        Assert.True(snapshot.ObservationPresentationOnly);
    }

    [Fact]
    public void 전달완료지만_수령확인전인주문은_완료건수에서제외한다()
    {
        var source = 세션();
        source.FoodDeliveries[0].StateCode = 음식배달상태코드.전달완료;
        source.FoodDeliveries[0].ReceivedTick = null;

        var snapshot = new 사가정합성기사관찰SnapshotFactory()
            .생성(source);

        Assert.Equal(0, snapshot.CompletedDeliveryCount);
        Assert.Equal(1, snapshot.InProgressDeliveryCount);
    }

    [Fact]
    public void 대기단계는_현재배정과경로비율을추정하지않는다()
    {
        var source = 세션();
        source.SyntheticCourier!.Stage = "Idle";
        source.SyntheticCourier.OrderStableId = string.Empty;
        source.SyntheticCourier.Distance = 0;
        source.SyntheticCourier.Carrying = false;

        var snapshot = new 사가정합성기사관찰SnapshotFactory()
            .생성(source);

        Assert.Empty(snapshot.CurrentOrderStableId);
        Assert.Empty(snapshot.CurrentOrderStateCode);
        Assert.Equal(0, snapshot.InProgressDeliveryCount);
        Assert.Equal(사가정합성기사관찰RouteProgressStatusCodes
            .NotApplicable, snapshot.RouteProgressStatusCode);
        Assert.Equal(0, snapshot.RouteLengthMeters);
        Assert.Equal(0, snapshot.RouteProgressMeters);
        Assert.Null(snapshot.RouteProgressRatio);
    }

    [Fact]
    public void 낮은Revision_기사불일치_수령형태불일치를_각각거절한다()
    {
        var factory = new 사가정합성기사관찰SnapshotFactory();
        var low = Assert.Throws<InvalidDataException>(() =>
            factory.생성(세션(), 23));
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes.StaleRevision,
            low.Message);

        var actorMismatch = 세션();
        actorMismatch.SyntheticCourier!.ActorStableId =
            "actor:synthetic-courier:2";
        var actor = Assert.Throws<InvalidDataException>(() =>
            factory.생성(actorMismatch));
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes.ActorMismatch,
            actor.Message);

        var malformed = 세션();
        malformed.FoodDeliveries[0].ReceivedTick = null;
        var shape = Assert.Throws<InvalidDataException>(() =>
            factory.생성(malformed));
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes
            .SnapshotShapeInvalid, shape.Message);

        var operational = 세션();
        operational.IsOperationalState = true;
        var forbidden = Assert.Throws<InvalidDataException>(() =>
            factory.생성(operational));
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes
            .OperationalSourceForbidden, forbidden.Message);
    }

    [Fact]
    public void 메모리투영은_복사본만보존하고_낮은Revision과충돌을거절한다()
    {
        var factory = new 사가정합성기사관찰SnapshotFactory();
        var first = factory.생성(세션());
        var projector = new 사가정합성기사관찰상태Projector();

        var applied = projector.적용(first, first.SessionStableId,
            사가정합성기사관찰Policy.ActorStableId);
        Assert.True(applied.Accepted);
        Assert.True(applied.Changed);
        applied.Snapshot!.ActorX = -999;
        Assert.Equal(20, projector.Current!.ActorX);

        var unchanged = projector.적용(first, first.SessionStableId,
            사가정합성기사관찰Policy.ActorStableId);
        Assert.True(unchanged.Accepted);
        Assert.False(unchanged.Changed);

        var wrongStation = projector.적용(first, first.SessionStableId,
            "station:kr:kric:s1107:0723",
            사가정합성기사관찰Policy.ActorStableId);
        Assert.False(wrongStation.Accepted);
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes
            .IdentityMismatch, wrongStation.ErrorCode);

        var lowerSource = 세션();
        판본(lowerSource, 21, 74);
        var stale = projector.적용(factory.생성(lowerSource),
            first.SessionStableId,
            사가정합성기사관찰Policy.ActorStableId);
        Assert.False(stale.Accepted);
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes.StaleRevision,
            stale.ErrorCode);

        var conflictSource = 세션();
        conflictSource.SyntheticCourier!.Distance = 20;
        conflictSource.SyntheticCourier.X = 20;
        conflictSource.SyntheticCourier.Z = 10;
        conflictSource.SyntheticCourier.VehicleX = 20;
        conflictSource.SyntheticCourier.VehicleZ = 10;
        var conflict = projector.적용(factory.생성(conflictSource),
            first.SessionStableId,
            사가정합성기사관찰Policy.ActorStableId);
        Assert.False(conflict.Accepted);
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes
            .RevisionConflict, conflict.ErrorCode);
        Assert.Equal(15, projector.Current!.RouteProgressMeters);

        projector.Clear();
        Assert.Null(projector.Current);
    }

    [Fact]
    public void wire변조는_공유Fingerprint검증에서거절한다()
    {
        var snapshot = new 사가정합성기사관찰SnapshotFactory()
            .생성(세션());
        var validator = new 사가정합성기사관찰SnapshotValidator();
        Assert.True(validator.검증(snapshot).Accepted);
        Assert.Equal(snapshot.ProjectionFingerprintSha256,
            사가정합성기사관찰Fingerprint.Compute(snapshot));

        snapshot.ActorX += 1;
        var tampered = validator.검증(snapshot);

        Assert.False(tampered.Accepted);
        Assert.Equal(사가정합성기사관찰ProjectionErrorCodes
            .FingerprintMismatch, tampered.ErrorCode);
    }

    private static 경영SimulationSessionSnapshot 세션()
    {
        var result = new 경영SimulationSessionSnapshot
        {
            SessionStableId = "simulation-session:sagajeong-courier-card",
            ScenarioStableId = 사가정합성기사관찰Policy.ScenarioStableId,
            ScenarioDataRevision = 사가정합성기사관찰Policy
                .ScenarioDataRevision,
            CurrentTick = 75,
            DurationTicks = 200,
            Revision = 22,
            ModeCode = SimulationModeCodes.Simulation,
            IsOperationalState = false,
            WorldContext = new SimulationWorldContextSnapshot
            {
                WorldTick = 75,
                WorldRevision = 22
            },
            SyntheticCourier = new 가상배달기사Snapshot
            {
                ActorStableId = 사가정합성기사관찰Policy.ActorStableId,
                Stage = "DriveHome",
                OrderStableId = "food-order:synthetic:2:a",
                Batch = 2,
                Distance = 15,
                X = 20,
                Z = 5,
                VehicleX = 20,
                VehicleZ = 5,
                Carrying = true
            },
            FoodDeliveries = new[]
            {
                주문("food-order:synthetic:1:a",
                    음식배달상태코드.수령확인, 1,
                    cooking: 2, ready: 3, dispatch: 4, pickup: 5,
                    delivered: 6, received: 7),
                주문("food-order:synthetic:2:a",
                    음식배달상태코드.픽업완료, 20,
                    cooking: 21, ready: 22, dispatch: 23, pickup: 24),
                주문("food-order:synthetic:2:b",
                    음식배달상태코드.주문대기, 70)
            }
        };
        return result;
    }

    private static Simulation음식배달Snapshot 주문(
        string id,
        string state,
        int accepted,
        int? cooking = null,
        int? ready = null,
        int? dispatch = null,
        int? pickup = null,
        int? delivered = null,
        int? received = null)
        => new()
        {
            FoodOrderStableId = id,
            DeliveryScopeStableId = 사가정합성기사관찰Policy
                .DeliveryScopeStableId,
            StateCode = state,
            Revision = 1,
            AcceptedTick = accepted,
            CookingStartedTick = cooking,
            ReadyForPickupTick = ready,
            DispatchCandidateTick = dispatch,
            PickedUpTick = pickup,
            DeliveredTick = delivered,
            ReceivedTick = received
        };

    private static void 판본(
        경영SimulationSessionSnapshot source,
        long revision,
        int tick)
    {
        source.Revision = revision;
        source.CurrentTick = tick;
        source.WorldContext.WorldRevision = revision;
        source.WorldContext.WorldTick = tick;
    }
}
