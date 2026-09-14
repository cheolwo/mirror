using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "SyntheticFixture 배달 여정의 graph 결속·결정성·진행·차단·신선도를 검증한다.",
    Boundary = "Unity 읽기 전용 투영 시험이며 운영 주문·실제 사가정 통행·Scene·Game View 증거가 아니다.")]
public sealed class 음식배달여정ProjectionTests
{
    private const string RegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
    private const string AdministrativeAreaStableId = "region:kr:hjd:1126057500";
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 9, 13, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Wire이동수단은_문자열이고_기존ordinal을보존한다()
    {
        Assert.Equal(0, (int)음식배달여정이동수단.Vehicle);
        Assert.Equal(1, (int)음식배달여정이동수단.Pedestrian);
        Assert.Equal(2, (int)음식배달여정이동수단.Motorcycle);

        var json = JsonSerializer.Serialize(new 음식배달여정Leg
        { Mode = 음식배달여정ModeCodes.Vehicle },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"mode\":\"Vehicle\"", json);
        Assert.DoesNotContain("\"mode\":0", json);
    }

    [Fact]
    public void 합성주문과기사를_다섯이동과복귀Leg여정으로_결정적투영한다()
    {
        var graph = 지도();
        var order = 주문();
        var courier = 기사("DriveHome", 15, 20, 5, 20, 5);
        var beforeOrder = JsonSerializer.Serialize(order);
        var beforeCourier = JsonSerializer.Serialize(courier);
        var request = 요청(graph, 12);
        var factory = new 음식배달여정SnapshotFactory();

        var first = factory.합성상태에서생성(graph, order, courier, request);
        var second = factory.합성상태에서생성(graph, order, courier, request);

        Assert.Equal(음식배달여정Policy.SchemaVersion, first.SchemaVersion);
        Assert.Equal(RegionStableId, first.RegionStableId);
        Assert.Equal(AdministrativeAreaStableId, first.AdministrativeAreaStableId);
        Assert.Equal(graph.Source.SourceVersion, first.GraphRevision);
        Assert.Equal(graph.Revision, first.GraphHash);
        Assert.Equal(64, first.RouteFingerprint.Length);
        Assert.Equal(사가정저밀도교통검증Fixture.CourierRouteFingerprint,
            first.RouteFingerprint);
        Assert.Equal(first.RouteFingerprint, second.RouteFingerprint);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.False(first.DistributionApproved);
        Assert.False(first.Blocked);
        Assert.Equal(음식배달여정ReadinessCodes.Ready, first.ReadinessCode);
        Assert.Equal(음식배달여정Policy.SyntheticFixtureSourceKind, first.SourceKindCode);
        Assert.Equal(3, first.CurrentLegIndex);
        Assert.Equal(7, first.Legs.Length);
        Assert.All(first.Legs.Take(3), leg => Assert.Equal(음식배달여정LegStateCodes.Completed, leg.StateCode));
        var current = first.Legs[3];
        Assert.Equal(음식배달여정ModeCodes.Motorcycle, current.Mode);
        Assert.Equal(음식배달여정LegStateCodes.Active, current.StateCode);
        Assert.Equal(40, current.DistanceMeters);
        Assert.Equal(15, current.ProgressMeters);
        Assert.Equal((20d, 5d), (current.ActorPosition.X, current.ActorPosition.Z));
        Assert.NotNull(current.VehiclePosition);
        Assert.Equal((20d, 5d), (current.VehiclePosition!.X, current.VehiclePosition.Z));
        Assert.Equal(JsonSerializer.Serialize(order), beforeOrder);
        Assert.Equal(JsonSerializer.Serialize(courier), beforeCourier);
        Assert.True(음식배달여정Policy.UnityReadOnly);
        Assert.False(음식배달여정Policy.CanonicalOrderMutationAllowed);
    }

    [Fact]
    public void 도착여정을적용해도_원본주문상태와Revision은변하지않는다()
    {
        var graph = 지도();
        var order = 주문();
        order.StateCode = "픽업완료";
        order.Revision = 7;
        var before = JsonSerializer.Serialize(order);
        var courier = 기사("Deliver", 0, 30, 24, 30, 20);
        var snapshot = new 음식배달여정SnapshotFactory()
            .합성상태에서생성(graph, order, courier, 요청(graph, 20));
        var projector = new 음식배달여정상태Projector();

        var applied = 적용(projector, snapshot, graph, ObservedAt.AddSeconds(1));

        Assert.True(applied.Accepted);
        Assert.True(applied.Changed);
        Assert.Equal(음식배달여정LegStateCodes.Completed, snapshot.Legs[snapshot.CurrentLegIndex].StateCode);
        Assert.Equal(before, JsonSerializer.Serialize(order));
        Assert.Equal(7, order.Revision);
        Assert.Equal("픽업완료", order.StateCode);
    }

    [Fact]
    public void 상태투영은_낮은Revision과같은Revision충돌을거절하고_복사본만보존한다()
    {
        var graph = 지도();
        var order = 주문();
        var factory = new 음식배달여정SnapshotFactory();
        var projector = new 음식배달여정상태Projector();
        var first = factory.합성상태에서생성(
            graph, order, 기사("DriveHome", 15, 20, 5, 20, 5), 요청(graph, 12));

        var applied = 적용(projector, first, graph, ObservedAt.AddSeconds(1));
        Assert.True(applied.Accepted);
        Assert.True(applied.Changed);
        applied.Snapshot!.Legs[3].ActorPosition.X = -999;
        Assert.Equal(20, projector.Current!.Legs[3].ActorPosition.X);
        Assert.False(적용(projector, first, graph, ObservedAt.AddSeconds(1)).Changed);

        var lower = factory.합성상태에서생성(
            graph, order, 기사("DriveHome", 10, 20, 0, 20, 0), 요청(graph, 11));
        var stale = 적용(projector, lower, graph, ObservedAt.AddSeconds(1));
        Assert.False(stale.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.StaleRevision, stale.ErrorCode);

        var conflict = factory.합성상태에서생성(
            graph, order, 기사("DriveHome", 20, 20, 10, 20, 10), 요청(graph, 12));
        var rejected = 적용(projector, conflict, graph, ObservedAt.AddSeconds(1));
        Assert.False(rejected.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.RevisionConflict, rejected.ErrorCode);
        Assert.Equal(15, projector.Current!.Legs[3].ProgressMeters);
        projector.Clear();
        Assert.Null(projector.Current);
    }

    [Fact]
    public void 오래된사본과_graphHash불일치와_경로지문변조를거절한다()
    {
        var graph = 지도();
        var snapshot = new 음식배달여정SnapshotFactory().합성상태에서생성(
            graph, 주문(), 기사("DriveHome", 15, 20, 5, 20, 5), 요청(graph, 12));
        var validator = new 음식배달여정SnapshotValidator();

        var stale = validator.검증(snapshot, RegionStableId, AdministrativeAreaStableId,
            graph.Source.SourceVersion, graph.Revision, ObservedAt.AddSeconds(31), TimeSpan.FromSeconds(30));
        Assert.False(stale.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.StaleSnapshot, stale.ErrorCode);

        var mismatch = validator.검증(snapshot, RegionStableId, AdministrativeAreaStableId,
            graph.Source.SourceVersion, new string('B', 64), ObservedAt.AddSeconds(1), TimeSpan.FromSeconds(30));
        Assert.False(mismatch.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.GraphHashMismatch, mismatch.ErrorCode);

        snapshot.Legs[3].Points[1].X += 1;
        var tampered = validator.검증(snapshot, RegionStableId, AdministrativeAreaStableId,
            graph.Source.SourceVersion, graph.Revision, ObservedAt.AddSeconds(1), TimeSpan.FromSeconds(30));
        Assert.False(tampered.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.SnapshotShapeInvalid, tampered.ErrorCode);
    }

    [Fact]
    public void 검토된연결이없으면_직선Fallback없이_미해결차단사본을만든다()
    {
        var graph = 지도(blockFirstRoad: true);
        var snapshot = new 음식배달여정SnapshotFactory().합성상태에서생성(
            graph, 주문(), 기사("DriveRestaurant", 5, 5, 0, 5, 0), 요청(graph, 2));

        Assert.True(snapshot.Blocked);
        Assert.Equal(음식배달여정ReadinessCodes.RouteUnresolved, snapshot.ReadinessCode);
        Assert.Equal(0, snapshot.CurrentLegIndex);
        var blocked = snapshot.Legs[0];
        Assert.Equal(음식배달여정LegStateCodes.Blocked, blocked.StateCode);
        Assert.Empty(blocked.Points);
        Assert.Equal(0, blocked.DistanceMeters);
        Assert.Equal((5d, 0d), (blocked.ActorPosition.X, blocked.ActorPosition.Z));
        Assert.NotNull(blocked.VehiclePosition);
        Assert.Equal((5d, 0d), (blocked.VehiclePosition!.X, blocked.VehiclePosition.Z));
        var validation = new 음식배달여정SnapshotValidator().검증(
            snapshot, RegionStableId, AdministrativeAreaStableId, graph.Source.SourceVersion,
            graph.Revision, ObservedAt.AddSeconds(1), TimeSpan.FromSeconds(30));
        Assert.True(validation.Accepted);
    }

    [Fact]
    public void 생성단계에서_graph판본과Hash불일치를각각거절한다()
    {
        var graph = 지도();
        var request = 요청(graph, 1);
        request.ExpectedGraphRevision = "synthetic-spatial.r0";
        var revision = Assert.Throws<InvalidDataException>(() => new 음식배달여정SnapshotFactory()
            .합성상태에서생성(graph, 주문(), 기사("DriveRestaurant", 0, 0, 0, 0, 0), request));
        Assert.Equal(음식배달여정ProjectionErrorCodes.GraphRevisionMismatch, revision.Message);

        request = 요청(graph, 1);
        request.ExpectedGraphHash = new string('0', 64);
        var hash = Assert.Throws<InvalidDataException>(() => new 음식배달여정SnapshotFactory()
            .합성상태에서생성(graph, 주문(), 기사("DriveRestaurant", 0, 0, 0, 0, 0), request));
        Assert.Equal(음식배달여정ProjectionErrorCodes.GraphHashMismatch, hash.Message);
    }

    [Theory]
    [InlineData("region:kr:hjd:1126057500", AdministrativeAreaStableId)]
    [InlineData(RegionStableId, "region:kr:bjd:1126010100")]
    [InlineData(RegionStableId, "region:kr:hjd:112605750")]
    public void Validator는_region과행정동식별자형식을구분한다(string region, string administrativeArea)
    {
        var graph = 지도();
        var snapshot = new 음식배달여정SnapshotFactory().합성상태에서생성(
            graph, 주문(), 기사("DriveRestaurant", 0, 0, 0, 0, 0), 요청(graph, 1));
        snapshot.RegionStableId = region;
        snapshot.AdministrativeAreaStableId = administrativeArea;

        var result = new 음식배달여정SnapshotValidator().검증(
            snapshot, RegionStableId, AdministrativeAreaStableId, graph.Source.SourceVersion,
            graph.Revision, ObservedAt.AddSeconds(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Accepted);
        Assert.Equal(음식배달여정ProjectionErrorCodes.SnapshotShapeInvalid, result.ErrorCode);
    }

    private static 음식배달여정ApplyResult 적용(
        음식배달여정상태Projector projector,
        음식배달여정Snapshot snapshot,
        동네공간Snapshot graph,
        DateTimeOffset now)
        => projector.적용(snapshot, RegionStableId, AdministrativeAreaStableId,
            graph.Source.SourceVersion, graph.Revision, now, TimeSpan.FromSeconds(30));

    private static 합성음식배달여정ProjectionRequest 요청(동네공간Snapshot graph, long revision)
        => new()
        {
            RegionStableId = RegionStableId,
            AdministrativeAreaStableId = AdministrativeAreaStableId,
            VehicleStableId = "vehicle:synthetic-courier:1",
            RouteStableId = "route:synthetic:sagajeong-food-delivery:a",
            ExpectedGraphRevision = graph.Source.SourceVersion,
            ExpectedGraphHash = graph.Revision,
            Revision = revision,
            ObservedAt = ObservedAt
        };

    private static Simulation음식배달Snapshot 주문()
        => new()
        {
            FoodOrderStableId = "food-order:synthetic:1:a",
            DestinationFacilityStableId = "facility:synthetic:a",
            DeliveryScopeStableId = "delivery-scope:synthetic",
            StateCode = "기사배정",
            Revision = 6
        };

    private static 가상배달기사Snapshot 기사(
        string stage, double distance, double x, double z, double vehicleX, double vehicleZ)
        => new()
        {
            ActorStableId = "actor:synthetic-courier:1",
            OrderStableId = "food-order:synthetic:1:a",
            Stage = stage,
            Distance = distance,
            X = x,
            Z = z,
            VehicleX = vehicleX,
            VehicleZ = vehicleZ
        };

    private static 동네공간Snapshot 지도(bool blockFirstRoad = false)
    {
        using var stream = typeof(음식배달여정ProjectionTests).Assembly
            .GetManifestResourceStream("Ssalddel.Simulation.Tests.synthetic-delivery-block.v1.json")!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var json = Encoding.UTF8.GetString(memory.ToArray())
            .Replace("\"modes\": [\"Vehicle\"]",
                "\"modes\": [\"Vehicle\",\"Motorcycle\"]", StringComparison.Ordinal);
        if (blockFirstRoad)
        {
            const string reviewed = "\"accessReview\": \"Reviewed\"";
            var index = json.IndexOf(reviewed, StringComparison.Ordinal);
            Assert.True(index >= 0);
            json = json.Substring(0, index) + "\"accessReview\": \"Blocked\""
                + json.Substring(index + reviewed.Length);
        }
        var bytes = Encoding.UTF8.GetBytes(json);
        return new 동네공간ImportService().읽기(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
