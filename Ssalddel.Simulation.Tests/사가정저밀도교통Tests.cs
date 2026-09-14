using System.Text.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "사가정 저밀도 합성 신호·대기열·연속 배달 후보 경로의 결정성과 비권위 경계를 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3계약회귀,
    Boundary = "순수 Core 시험이며 실제 신호 주기·OSM 통행 승인·Hosted·Unity Play Mode 증거가 아니다.")]
public sealed class 사가정저밀도교통Tests
{
    private readonly 사가정저밀도교통Engine engine = new();

    [Fact]
    public void 승인된저밀도Profile은_오토바이하나와주변차량셋만만든다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();

        var snapshot = engine.생성(input, 0);

        Assert.Equal(사가정저밀도교통Policy.SchemaVersion, snapshot.SchemaVersion);
        Assert.Equal(4, snapshot.Actors.Length);
        Assert.Single(snapshot.Actors, actor =>
            actor.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(3, snapshot.Actors.Count(actor =>
            actor.KindCode == 사가정저밀도교통ActorKindCodes.AmbientPassengerCar));
        Assert.Equal(2, snapshot.Signals.Length);
        Assert.Equal(4, snapshot.Lanes.Length);
        Assert.Equal(.25d, snapshot.TickSeconds);
        Assert.Equal(20260913, snapshot.ScenarioSeed);
        Assert.Equal(사가정저밀도교통검증Fixture.CourierJourneyId,
            snapshot.CourierJourneyId);
        Assert.Equal(사가정저밀도교통검증Fixture.CourierRouteFingerprint,
            snapshot.RouteFingerprint);
        Assert.Equal(사가정저밀도교통검증Fixture.CourierDisplayRouteRevision,
            snapshot.CourierDisplayRouteRevision);
        Assert.Equal(사가정저밀도교통검증Fixture.SourceMobilityGraphStableId,
            snapshot.SourceMobilityGraphStableId);
        Assert.Equal(사가정저밀도교통검증Fixture.SourceMobilityGraphRevision,
            snapshot.SourceMobilityGraphRevision);
        Assert.Equal(사가정저밀도교통검증Fixture.SourceMobilityGraphProjectionHashSha256,
            snapshot.SourceMobilityGraphProjectionHashSha256);
        Assert.Equal(사가정저밀도교통검증Fixture.SourceMobilityGraphContentHashSha256,
            snapshot.SourceMobilityGraphContentHashSha256);
        Assert.Equal(64, snapshot.CourierDisplayRouteFingerprint.Length);
        Assert.Equal(사가정저밀도교통검증Fixture.CourierDisplayRouteFingerprint,
            snapshot.CourierDisplayRouteFingerprint);
        Assert.Equal(3, snapshot.CourierRouteLegs.Length);
        Assert.Equal(15, snapshot.CourierRouteLegs.Sum(value => value.SourceEdges.Length));
        Assert.Equal(new[]
        {
            "mobility-tile:sagajeong:x0:z1.r1:osm-way:218790209:segment:21:part:0",
            "mobility-tile:sagajeong:x0:z0.r1:osm-way:218790209:segment:21:part:1",
            "mobility-tile:sagajeong:x0:z0.r1:osm-way:218790209:segment:22:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:22:part:1",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:23:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:24:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:25:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326984:segment:0:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326970:segment:1:part:0",
            "mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326970:segment:0:part:1",
            "mobility-tile:sagajeong:x1:z1.r1:osm-way:1112326970:segment:0:part:0",
            "mobility-tile:sagajeong:x1:z1.r1:osm-way:576294786:segment:0:part:0",
            "mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:0:part:0",
            "mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:1:part:0",
            "mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:2:part:0"
        }, snapshot.CourierRouteLegs.SelectMany(value => value.SourceEdges)
            .Select(value => value.EdgeStableId));
        Assert.Equal(3, snapshot.CourierRouteLegs.SelectMany(value => value.SourceEdges)
            .Count(value => value.DisplayGeometryOrderCode ==
                            사가정저밀도교통SourceGeometryOrderCodes.ReverseStored));
        Assert.Equal(277.276d,
            snapshot.CourierRouteLegs.Sum(value => value.SourceDistanceMeters), 6);
        Assert.Equal(277.277388d,
            snapshot.CourierRouteLegs.Sum(value => value.DisplayGeometryDistanceMeters), 6);
        Assert.All(snapshot.CourierRouteLegs.SelectMany(value => value.SourceEdges), edge =>
        {
            Assert.Equal(사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                edge.SourceDirectionCode);
            Assert.Equal(사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                edge.AccessReviewCode);
            Assert.False(edge.RuntimeAuthorized);
        });
        Assert.Equal(3, snapshot.CourierRouteState.RouteLegCount);
        Assert.Equal(277.277388d, snapshot.CourierRouteState.TotalDistanceMeters, 6);
        Assert.False(snapshot.CourierRouteState.CandidateEndpointReached);
        Assert.Equal(64, snapshot.InputStateFingerprint.Length);
        Assert.True(snapshot.ObservationPresentationOnly);
        Assert.False(snapshot.DistributionApproved);
        Assert.False(snapshot.TraversalReady);
        Assert.False(snapshot.GameplayReady);
        Assert.Equal(
            사가정저밀도교통SourceAuthorityCodes.DeclaredSyntheticReferenceOnly,
            snapshot.JourneyBindingStateCode);
        Assert.False(snapshot.OperationalBindingReady);
        Assert.False(snapshot.CanonicalDeliveryMutationAllowed);
        Assert.Equal(사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
            snapshot.SourceDirectionAuthorityCode);
        Assert.Equal(사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
            snapshot.SourceAccessReviewCode);
        Assert.False(snapshot.SourceRuntimeAuthorized);
        Assert.All(snapshot.Lanes, lane =>
        {
            Assert.Equal(사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                lane.SourceDirectionCode);
            Assert.Equal(사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                lane.SourceAccessReviewCode);
            Assert.False(lane.SourceRuntimeAuthorized);
        });
        Assert.All(snapshot.Actors.Where(actor =>
                actor.KindCode == 사가정저밀도교통ActorKindCodes.AmbientPassengerCar),
            actor => Assert.NotEqual(
                사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate,
                actor.RouteKindCode));
    }

    [Fact]
    public void 같은입력Seed와Tick은_바이트동등한상태사본을만든다()
    {
        var first = engine.생성(사가정저밀도교통검증Fixture.만들기(), 73);
        var second = engine.생성(사가정저밀도교통검증Fixture.만들기(), 73);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(74, first.Revision);
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 0, 0, 18, 250,
            TimeSpan.Zero), first.ObservedAt);
    }

    [Fact]
    public void 합성신호는_28초주기의녹색황색전방향적색을재현한다()
    {
        AssertSignal(0, "Green", "Red");
        AssertSignal(40, "Amber", "Red");
        AssertSignal(48, "Red", "Red");
        AssertSignal(56, "Red", "Green");
        AssertSignal(96, "Red", "Amber");
        AssertSignal(104, "Red", "Red");
        AssertSignal(112, "Green", "Red");
    }

    [Fact]
    public void 빨간신호에는기사도정지선뒤대기열에합류한다()
    {
        var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(), 40);
        var actors = snapshot.Actors.Where(actor =>
                actor.LaneStableId == 사가정저밀도교통검증Fixture.EastWestLane)
            .OrderByDescending(actor => actor.ProgressMeters).ToArray();

        Assert.Equal(3, actors.Length);
        Assert.Equal(사가정저밀도교통ActorStateCodes.WaitingAtSignal,
            actors[0].StateCode);
        Assert.All(actors.Skip(1), actor => Assert.Equal(
            사가정저밀도교통ActorStateCodes.Queueing, actor.StateCode));
        Assert.Equal(new[] { 0, 1, 2 }, actors.Select(value => value.QueueIndex));
        Assert.All(actors.Zip(actors.Skip(1)), pair =>
            Assert.Equal(6d, pair.First.ProgressMeters - pair.Second.ProgressMeters, 6));
        Assert.Equal(52.59d - 1.5d, actors[0].ProgressMeters, 6);
        Assert.Equal(2, actors.Single(value => value.KindCode ==
            사가정저밀도교통ActorKindCodes.CourierMotorcycle).QueueIndex);
    }

    [Fact]
    public void 초록신호에는앞차부터같은간격으로다시출발한다()
    {
        var stopped = engine.생성(사가정저밀도교통검증Fixture.만들기(), 56)
            .Actors.Where(actor => actor.LaneStableId ==
                                   사가정저밀도교통검증Fixture.EastWestLane)
            .OrderBy(actor => actor.StableId).ToArray();
        var moving = engine.생성(사가정저밀도교통검증Fixture.만들기(), 60)
            .Actors.Where(actor => actor.LaneStableId ==
                                   사가정저밀도교통검증Fixture.EastWestLane)
            .OrderBy(actor => actor.StableId).ToArray();

        Assert.All(moving, actor =>
            Assert.Equal(사가정저밀도교통ActorStateCodes.Moving, actor.StateCode));
        Assert.All(moving, actor => Assert.True(actor.SpeedMetersPerSecond > 0d));
        Assert.All(moving.Zip(stopped), pair =>
            Assert.True(pair.First.ProgressMeters > pair.Second.ProgressMeters));
        var ordered = moving.OrderByDescending(actor => actor.ProgressMeters).ToArray();
        Assert.All(ordered.Zip(ordered.Skip(1)), pair =>
            Assert.Equal(6d, pair.First.ProgressMeters - pair.Second.ProgressMeters, 6));
    }

    [Fact]
    public void 기사는대로신호에서생활도로와골목을건너후보종점에정차한다()
    {
        var neighborhood = FirstCourierSnapshot(value => value.CourierRouteState
            .CurrentRouteLegIndex == 1);
        Assert.Equal(사가정저밀도교통검증Fixture.NeighborhoodAccessLane,
            neighborhood.CourierRouteState.CurrentLaneStableId);
        Assert.Equal(사가정저밀도교통JourneyStageCodes.NeighborhoodRoadApproach,
            neighborhood.CourierRouteState.StageCode);

        var alley = FirstCourierSnapshot(value => value.CourierRouteState
            .CurrentRouteLegIndex == 2);
        Assert.Equal(사가정저밀도교통검증Fixture.AlleyLane,
            alley.CourierRouteState.CurrentLaneStableId);
        Assert.Equal(사가정저밀도교통JourneyStageCodes.AlleyFinalApproach,
            alley.CourierRouteState.StageCode);

        var arrived = FirstCourierSnapshot(value =>
            value.CourierRouteState.CandidateEndpointReached);
        var courier = arrived.Actors.Single(actor => actor.KindCode ==
            사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(사가정저밀도교통ActorStateCodes.StoppedAtCandidateEndpoint,
            courier.StateCode);
        Assert.Equal(0d, courier.SpeedMetersPerSecond);
        Assert.Equal(arrived.CourierRouteState.TotalDistanceMeters,
            arrived.CourierRouteState.CumulativeProgressMeters, 6);
        Assert.Equal(사가정저밀도교통JourneyStageCodes.CandidateEndpointReached,
            arrived.CourierRouteState.StageCode);

        var occupied = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [사가정저밀도교통검증Fixture.AlleyResource] = "actor:synthetic:blocker:1"
        };
        var waitingSnapshot = FirstCourierSnapshot(value => value.Actors.Any(actor =>
                actor.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle
                && actor.StateCode == 사가정저밀도교통ActorStateCodes.WaitingForAlley),
            occupied);
        var waiting = waitingSnapshot.Actors.Single(actor => actor.KindCode ==
            사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(0d, waiting.ProgressMeters);
        Assert.Equal(0d, waiting.SpeedMetersPerSecond);
        Assert.Equal(사가정저밀도교통ActorStateCodes.WaitingForAlley,
            waiting.StateCode);
        Assert.Equal("ResourceOccupied", waiting.BlockedReasonCode);
        Assert.Equal("actor:synthetic:blocker:1", occupied[사가정저밀도교통검증Fixture.AlleyResource]);
    }

    [Fact]
    public void 원본차로Hash와Profile지문변조는_부분적용전에거부한다()
    {
        var laneTampered = 사가정저밀도교통검증Fixture.만들기();
        laneTampered.Lanes[0].Points[0].X += 1d;
        var laneError = Assert.Throws<InvalidDataException>(() =>
            engine.생성(laneTampered, 1));
        Assert.Equal("LaneGraphHashMismatch", laneError.Message);

        var configTampered = 사가정저밀도교통검증Fixture.만들기();
        configTampered.Profile.AlleySpeedMetersPerSecond = 3d;
        var configError = Assert.Throws<InvalidDataException>(() =>
            engine.생성(configTampered, 1));
        Assert.Equal("TrafficConfigurationFingerprintMismatch", configError.Message);
    }

    [Fact]
    public void 음수나과도한Tick과_골목주변차량은거부한다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.생성(input, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.생성(input,
            사가정저밀도교통Engine.MaximumSimulationTick + 1));

        input = 사가정저밀도교통검증Fixture.만들기();
        input.Actors.First(actor => actor.KindCode ==
                                    사가정저밀도교통ActorKindCodes.AmbientPassengerCar)
            .LaneStableId = 사가정저밀도교통검증Fixture.AlleyLane;
        input.ConfigurationFingerprint = 사가정저밀도교통Fingerprint.Configuration(input);
        var error = Assert.Throws<InvalidDataException>(() => engine.생성(input, 1));
        Assert.Equal("TrafficActorPlacementInvalid", error.Message);
    }

    [Fact]
    public void Actor입력순서는_같은지문과Tick의결과를바꾸지않는다()
    {
        var original = 사가정저밀도교통검증Fixture.만들기();
        var reordered = 사가정저밀도교통검증Fixture.만들기();
        reordered.Actors = reordered.Actors.Reverse().ToArray();
        Rehash(reordered);

        var first = engine.생성(original, 73);
        var second = engine.생성(reordered, 73);

        Assert.Equal(original.ConfigurationFingerprint, reordered.ConfigurationFingerprint);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void NaN과InfinityProfile및정지선은_상태사본전에거부한다()
    {
        var nanSpeed = 사가정저밀도교통검증Fixture.만들기();
        nanSpeed.Profile.MainRoadSpeedMetersPerSecond = double.NaN;
        Rehash(nanSpeed);
        Assert.Equal("TrafficProfileInvalid", Assert.Throws<InvalidDataException>(() =>
            engine.생성(nanSpeed, 1)).Message);

        var infiniteGap = 사가정저밀도교통검증Fixture.만들기();
        infiniteGap.Profile.MinimumGapMeters = double.PositiveInfinity;
        Rehash(infiniteGap);
        Assert.Equal("TrafficProfileInvalid", Assert.Throws<InvalidDataException>(() =>
            engine.생성(infiniteGap, 1)).Message);

        var nanStop = 사가정저밀도교통검증Fixture.만들기();
        nanStop.Lanes[0].StopLineProgressMeters = double.NaN;
        Rehash(nanStop);
        Assert.Equal("TrafficLaneInvalid", Assert.Throws<InvalidDataException>(() =>
            engine.생성(nanStop, 1)).Message);

        var infinitePoint = 사가정저밀도교통검증Fixture.만들기();
        infinitePoint.Lanes[0].Points[1].X = double.PositiveInfinity;
        Rehash(infinitePoint);
        Assert.Equal("TrafficLaneInvalid", Assert.Throws<InvalidDataException>(() =>
            engine.생성(infinitePoint, 1)).Message);
    }

    [Fact]
    public void AcceptedR2는_exactProfile과고정원천경로만수용한다()
    {
        var mutations = new Action<사가정저밀도교통Input>[]
        {
            value => value.Profile.TickSeconds = .5d,
            value =>
            {
                value.Profile.NorthSouthGreenSeconds = 9d;
                value.Profile.EastWestGreenSeconds = 11d;
            },
            value => value.Profile.MainRoadSpeedMetersPerSecond = 5d,
            value => value.SourceObservationRevision = "unapproved-source",
            value => value.Lanes[0].AxisCode = "UnknownAxis",
            value => value.Lanes[0].RouteKindCode = "UnknownRoute",
            value => value.Lanes[0].SourceWayStableId = "osm:way:unapproved"
        };
        foreach (var mutate in mutations)
        {
            var input = 사가정저밀도교통검증Fixture.만들기();
            mutate(input);
            Rehash(input);
            Assert.Contains(Assert.Throws<InvalidDataException>(() => engine.생성(input, 1)).Message,
                new[] { "LowDensityApprovedFixtureMismatch", "TrafficLaneAuthorityInvalid" });
        }
    }

    [Fact]
    public void 초기최소간격위반은_부분적용전에거부한다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();
        input.Actors.Single(value => value.StableId.EndsWith(":2", StringComparison.Ordinal))
            .InitialProgressMeters = 39d;
        Rehash(input);

        Assert.Equal("TrafficInitialGapInvalid",
            Assert.Throws<InvalidDataException>(() => engine.생성(input, 1)).Message);
    }

    [Fact]
    public void 열린주도로끝에서는_같은Actor가시작점으로순간이동하지않는다()
    {
        const string actorId = "actor:synthetic-ambient-car:sagajeong:1";
        var before = engine.생성(사가정저밀도교통검증Fixture.만들기(), 93);
        var after = engine.생성(사가정저밀도교통검증Fixture.만들기(), 94);

        Assert.Contains(before.Actors, value => value.StableId == actorId);
        Assert.DoesNotContain(after.Actors, value => value.StableId == actorId);
    }

    [Fact]
    public void 구분문자를포함한서로다른차로그래프는_서로다른Hash를가진다()
    {
        var first = new[] { HashLane("a|b", "c") };
        var second = new[] { HashLane("a", "b|c") };

        Assert.NotEqual(사가정저밀도교통Fingerprint.LaneGraph(first),
            사가정저밀도교통Fingerprint.LaneGraph(second));
    }

    [Fact]
    public void 점유상태와Epoch차이는_별도입력상태지문에반영된다()
    {
        var free = 사가정저밀도교통검증Fixture.만들기();
        var occupied = 사가정저밀도교통검증Fixture.만들기(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [사가정저밀도교통검증Fixture.AlleyResource] = "actor:synthetic:blocker:1"
            });
        var anotherEpoch = 사가정저밀도교통검증Fixture.만들기();
        anotherEpoch.EpochUtc = anotherEpoch.EpochUtc.AddSeconds(1);
        Rehash(anotherEpoch);

        Assert.Equal(free.ConfigurationFingerprint, occupied.ConfigurationFingerprint);
        Assert.NotEqual(free.InputStateFingerprint, occupied.InputStateFingerprint);
        Assert.NotEqual(free.InputStateFingerprint, anotherEpoch.InputStateFingerprint);
    }

    [Fact]
    public void 입력상태지문변조는_부분적용전에거부한다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();
        input.InputStateFingerprint = new string('A', 64);

        Assert.Equal("TrafficInputStateFingerprintMismatch",
            Assert.Throws<InvalidDataException>(() => engine.생성(input, 1)).Message);
    }

    [Fact]
    public void 표시경로지문변조는_기존음식배달경로지문과분리해거부한다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();
        var businessRouteFingerprint = input.RouteFingerprint;
        input.CourierDisplayRoute.Fingerprint = new string('A', 64);
        input.ConfigurationFingerprint = 사가정저밀도교통Fingerprint.Configuration(input);
        input.InputStateFingerprint = 사가정저밀도교통Fingerprint.InputState(input);

        Assert.Equal(사가정저밀도교통검증Fixture.CourierRouteFingerprint,
            businessRouteFingerprint);
        Assert.Equal("CourierDisplayRouteFingerprintMismatch",
            Assert.Throws<InvalidDataException>(() => engine.생성(input, 1)).Message);
    }

    [Fact]
    public void 연속경로상태는_lane전환을건너뛰지않고누적거리가감소하지않는다()
    {
        var previousIndex = 0;
        var previousProgress = 0d;
        for (var tick = 0L; tick <= 320L; tick++)
        {
            var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(), tick);
            var state = snapshot.CourierRouteState;
            var courier = snapshot.Actors.Single(value => value.KindCode ==
                사가정저밀도교통ActorKindCodes.CourierMotorcycle);
            var leg = snapshot.CourierRouteLegs[state.CurrentRouteLegIndex];

            Assert.InRange(state.CurrentRouteLegIndex, previousIndex, previousIndex + 1);
            Assert.True(state.CumulativeProgressMeters + .000001d >= previousProgress);
            Assert.Equal(leg.StableId, state.CurrentRouteLegStableId);
            Assert.Equal(leg.LaneStableId, state.CurrentLaneStableId);
            Assert.Equal(courier.LaneStableId, state.CurrentLaneStableId);
            Assert.Equal(courier.ProgressMeters, state.LegProgressMeters, 6);
            Assert.InRange(state.LegProgressMeters, 0d,
                leg.DisplayGeometryDistanceMeters + .000001d);
            Assert.InRange(state.CumulativeProgressMeters, 0d,
                state.TotalDistanceMeters + .000001d);
            previousIndex = state.CurrentRouteLegIndex;
            previousProgress = state.CumulativeProgressMeters;
        }
        Assert.Equal(2, previousIndex);
        Assert.Equal(277.277388d, previousProgress, 6);
    }

    [Fact]
    public void tick0부터400까지_대기상태의현재속도는0이고권위경계는유지된다()
    {
        for (var tick = 0L; tick <= 400L; tick++)
        {
            var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(), tick);
            Assert.False(snapshot.SourceRuntimeAuthorized);
            Assert.False(snapshot.TraversalReady);
            Assert.False(snapshot.GameplayReady);
            Assert.False(snapshot.OperationalBindingReady);
            Assert.False(snapshot.CanonicalDeliveryMutationAllowed);
            Assert.All(snapshot.Lanes, lane => Assert.False(lane.SourceRuntimeAuthorized));
            Assert.All(snapshot.CourierRouteLegs.SelectMany(leg => leg.SourceEdges),
                edge => Assert.False(edge.RuntimeAuthorized));

            foreach (var actor in snapshot.Actors.Where(actor =>
                         actor.StateCode ==
                         사가정저밀도교통ActorStateCodes.WaitingAtSignal
                         || actor.StateCode ==
                         사가정저밀도교통ActorStateCodes.Queueing
                         || actor.StateCode ==
                         사가정저밀도교통ActorStateCodes.WaitingForAlley
                         || actor.StateCode ==
                         사가정저밀도교통ActorStateCodes.StoppedAtCandidateEndpoint))
                Assert.Equal(0d, actor.SpeedMetersPerSecond);
        }
    }

    [Fact]
    public void 최대Tick에도기사는후보종점에정차하고업무완료를주장하지않는다()
    {
        var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(),
            사가정저밀도교통Engine.MaximumSimulationTick);
        var courier = snapshot.Actors.Single(value =>
            value.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(사가정저밀도교통ActorStateCodes.StoppedAtCandidateEndpoint,
            courier.StateCode);
        Assert.Equal(사가정저밀도교통DisplayPolylineOrderCodes.AsDefined,
            courier.DisplayPolylineOrderCode);
        Assert.True(snapshot.CourierRouteState.CandidateEndpointReached);
        Assert.False(snapshot.GameplayReady);

        var previous = engine.생성(사가정저밀도교통검증Fixture.만들기(),
            사가정저밀도교통Engine.MaximumSimulationTick - 1);
        var previousCourier = previous.Actors.Single(value =>
            value.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(previousCourier.Position.X, courier.Position.X, 6);
        Assert.Equal(previousCourier.Position.Z, courier.Position.Z, 6);
        Assert.Equal(645.625d, courier.Position.X, 3);
        Assert.Equal(89.089d, courier.Position.Z, 3);
    }

    [Fact]
    public void 신호전환순간에는정지하고_다음Tick부터출발한다()
    {
        var atTransition = engine.생성(사가정저밀도교통검증Fixture.만들기(), 56);
        var afterTransition = engine.생성(사가정저밀도교통검증Fixture.만들기(), 57);
        var id = "actor:synthetic-ambient-car:sagajeong:1";

        Assert.Equal(사가정저밀도교통SignalStateCodes.Green,
            atTransition.Signals.Single(value =>
                value.AxisCode == 사가정저밀도교통AxisCodes.EastWest).StateCode);
        Assert.Equal(사가정저밀도교통ActorStateCodes.WaitingAtSignal,
            atTransition.Actors.Single(value => value.StableId == id).StateCode);
        Assert.Equal(사가정저밀도교통ActorStateCodes.Moving,
            afterTransition.Actors.Single(value => value.StableId == id).StateCode);
    }

    [Fact]
    public void nullCollection은_통제된자료오류로거부한다()
    {
        var lanesMissing = 사가정저밀도교통검증Fixture.만들기();
        lanesMissing.Lanes = null!;
        Assert.Equal("TrafficCollectionMissing", Assert.Throws<InvalidDataException>(() =>
            engine.생성(lanesMissing, 1)).Message);

        var actorsMissing = 사가정저밀도교통검증Fixture.만들기();
        actorsMissing.Actors = null!;
        Assert.Equal("TrafficCollectionMissing", Assert.Throws<InvalidDataException>(() =>
            engine.생성(actorsMissing, 1)).Message);
    }

    [Fact]
    public void null이나빈표시경로는_배열접근예외대신통제오류로거부한다()
    {
        var routeMissing = 사가정저밀도교통검증Fixture.만들기();
        routeMissing.CourierDisplayRoute = null!;
        Assert.Equal("CourierDisplayRouteInvalid",
            Assert.Throws<InvalidDataException>(() => engine.생성(routeMissing, 1)).Message);

        var legsMissing = 사가정저밀도교통검증Fixture.만들기();
        legsMissing.CourierDisplayRoute.Legs = Array.Empty<
            사가정저밀도교통CourierRouteLegDefinition>();
        Assert.Equal("CourierDisplayRouteInvalid",
            Assert.Throws<InvalidDataException>(() => engine.생성(legsMissing, 1)).Message);
    }

    [Fact]
    public void 이동망계보와원천간선순서변조는_재지문화해도승인Fixture가아니다()
    {
        var provenance = 사가정저밀도교통검증Fixture.만들기();
        provenance.CourierDisplayRoute.SourceMobilityGraphContentHashSha256 =
            new string('A', 64);
        Rehash(provenance);
        Assert.Equal("LowDensityApprovedFixtureMismatch",
            Assert.Throws<InvalidDataException>(() => engine.생성(provenance, 1)).Message);

        var edgeOrder = 사가정저밀도교통검증Fixture.만들기();
        edgeOrder.CourierDisplayRoute.Legs[1].SourceEdges = edgeOrder.CourierDisplayRoute
            .Legs[1].SourceEdges.Reverse().ToArray();
        Rehash(edgeOrder);
        Assert.Equal("CourierDisplayRouteSourceEdgeInvalid",
            Assert.Throws<InvalidDataException>(() => engine.생성(edgeOrder, 1)).Message);
    }

    [Fact]
    public void 표시방향과OSM통행방향은_서로다른비권위필드로유지된다()
    {
        var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(), 1);
        var courier = snapshot.Actors.Single(value => value.KindCode ==
            사가정저밀도교통ActorKindCodes.CourierMotorcycle);
        Assert.Equal(사가정저밀도교통DisplayPolylineOrderCodes.AsDefined,
            courier.DisplayPolylineOrderCode);
        Assert.Equal(사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
            snapshot.SourceDirectionAuthorityCode);
        Assert.All(snapshot.CourierRouteLegs.SelectMany(value => value.SourceEdges),
            value => Assert.Equal(사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                value.SourceDirectionCode));
    }

    [Fact]
    public void 표시경로Fingerprint도구는_null원천점에통제오류를낸다()
    {
        var input = 사가정저밀도교통검증Fixture.만들기();
        input.CourierDisplayRoute.Legs[0].SourceEdges[0].DisplayFrom = null!;

        var error = Assert.Throws<ArgumentException>(() =>
            사가정저밀도교통Fingerprint.CourierDisplayRoute(
                input.CourierDisplayRoute, input.LaneGraphHash, input.Lanes));
        Assert.Equal("route", error.ParamName);
        Assert.StartsWith("CourierRouteSourceEdgePointMissing", error.Message,
            StringComparison.Ordinal);
    }

    private void AssertSignal(long tick, string expectedNorthSouth,
        string expectedEastWest)
    {
        var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(), tick);
        Assert.Equal(expectedNorthSouth, snapshot.Signals.Single(signal =>
            signal.AxisCode == 사가정저밀도교통AxisCodes.NorthSouth).StateCode);
        Assert.Equal(expectedEastWest, snapshot.Signals.Single(signal =>
            signal.AxisCode == 사가정저밀도교통AxisCodes.EastWest).StateCode);
    }

    private static void Rehash(사가정저밀도교통Input input)
    {
        if (input.Lanes != null)
            input.LaneGraphHash = 사가정저밀도교통Fingerprint.LaneGraph(input.Lanes);
        if (input.CourierDisplayRoute != null && input.Lanes != null)
            input.CourierDisplayRoute.Fingerprint =
                사가정저밀도교통Fingerprint.CourierDisplayRoute(
                    input.CourierDisplayRoute, input.LaneGraphHash, input.Lanes);
        input.ConfigurationFingerprint = 사가정저밀도교통Fingerprint.Configuration(input);
        input.InputStateFingerprint = 사가정저밀도교통Fingerprint.InputState(input);
    }

    private static 사가정저밀도교통LaneDefinition HashLane(string id, string source)
        => new()
        {
            StableId = id,
            SourceWayStableId = source,
            AxisCode = 사가정저밀도교통AxisCodes.EastWest,
            RouteKindCode = 사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly,
            StopLineProgressMeters = 1d,
            Points = new[]
            {
                new 사가정저밀도교통Point { X = 0d, Z = 0d },
                new 사가정저밀도교통Point { X = 2d, Z = 0d }
            }
        };

    private 사가정저밀도교통Snapshot FirstCourierSnapshot(
        Func<사가정저밀도교통Snapshot, bool> predicate,
        IReadOnlyDictionary<string, string>? occupied = null)
    {
        for (var tick = 0L; tick <= 400L; tick++)
        {
            var snapshot = engine.생성(사가정저밀도교통검증Fixture.만들기(occupied), tick);
            if (predicate(snapshot)) return snapshot;
        }
        throw new Xunit.Sdk.XunitException("CourierRouteStateNotObserved");
    }
}
