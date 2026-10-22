using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.Naver;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>격리 DB·생산용 본인 상세 Query와 외부 경로 대역으로 지도 접근/정보 최소화/최근 위치를 검증합니다.</summary>
public sealed class NeighborhoodDeliveryMapServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("other-owner")]
    public async Task 타인이나비로그인회원에게는_지도와외부경로호출을제공하지않는다(string? owner)
    {
        await using var f = await Fixture.Create();
        f.Current.UserId = owner;
        Assert.Null(await f.Service.내지도Async("delivery-1", true));
        Assert.Equal(0, f.Directions.Calls);
    }

    [Theory]
    [InlineData("legacy:")]
    [InlineData("neighborhood-delivery-evil:")]
    public async Task 기존화물이나유사prefix는_생활배송지도에노출하지않는다(string prefix)
    {
        await using var f = await Fixture.Create();
        f.Request.클라이언트요청Id = prefix + "1";
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Service.내지도Async("delivery-1", true));
        Assert.Equal(0, f.Directions.Calls);
    }

    [Fact]
    public async Task 기존진행조회권한이없으면_외부경로를호출하지않는다()
    {
        await using var f = await Fixture.Create();
        f.Deliveries.Deny = true;
        Assert.Null(await f.Service.내지도Async("delivery-1", true));
        Assert.Equal(0, f.Directions.Calls);
    }

    [Fact]
    public async Task 위치갱신조회는_경로API를호출하지않고_연락처나주소원문을포함하지않는다()
    {
        await using var f = await Fixture.Create();
        var map = await f.Service.내지도Async("delivery-1");
        Assert.NotNull(map);
        Assert.Equal(37.5m, map.Pickup!.Latitude);
        Assert.Equal(127.1m, map.Dropoff!.Longitude);
        Assert.Equal(NeighborhoodDeliveryMapStates.NotRequested, map.RouteStateCode);
        Assert.Null(map.PlannedRoute);
        Assert.Equal(0, f.Directions.Calls);
        var json = System.Text.Json.JsonSerializer.Serialize(map);
        Assert.DoesNotContain("비공개 상세 주소", json);
        Assert.DoesNotContain("010-1234-5678", json);
        Assert.DoesNotContain("비공개 담당자", json);
    }

    [Fact]
    public async Task 명시경로조회는_원장좌표로도로경로를조회하고_자동차참고출처와시각을표시한다()
    {
        await using var f = await Fixture.Create();
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.NotNull(map?.PlannedRoute);
        Assert.Equal((37.5m, 127m, 37.6m, 127.1m), f.Directions.LastInput);
        Assert.Equal(NeighborhoodDeliveryMapStates.Available, map.RouteStateCode);
        Assert.Equal(3, map.PlannedRoute.Points.Count);
        Assert.Equal(2.4m, map.PlannedRoute.DistanceKm);
        Assert.Equal(6m, map.PlannedRoute.DurationMinutes);
        Assert.Equal("AutomobileReference", map.PlannedRoute.TravelModeCode);
        Assert.Equal("NaverCloudDirections", map.PlannedRoute.SourceCode);
        Assert.Equal(f.Clock.Now, map.PlannedRoute.CalculatedAtUtc);
        Assert.Contains("실제 이동 기록이 아닙니다", map.PlannedRoute.Notice);
        await f.Service.내지도Async("delivery-1", true);
        Assert.Equal(2, f.Directions.Calls); // API 결과를 별도 캐시/DB화하지 않습니다.
        Assert.Empty(await f.Db.운송이벤트.Where(x => x.이벤트타입 != NeighborhoodDeliveryLocationPolicy.AssignmentEventType).ToListAsync());
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("exception")]
    [InlineData("no-path")]
    [InlineData("invalid-path")]
    public async Task 도로경로실패는_실선대체를만들지않고_핀과실패안내만유지한다(string failure)
    {
        await using var f = await Fixture.Create();
        f.Directions.Failure = failure;
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.NotNull(map?.Pickup);
        Assert.NotNull(map.Dropoff);
        Assert.Null(map.PlannedRoute);
        Assert.Equal(NeighborhoodDeliveryMapStates.Unavailable, map.RouteStateCode);
        Assert.DoesNotContain("private-provider-key", map.RouteNotice);
    }

    [Fact]
    public async Task 요청취소는_경로실패로숨기지않고_전파한다()
    {
        await using var f = await Fixture.Create();
        using var cancellation = new CancellationTokenSource();
        f.Directions.OnLookup = () => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.내지도Async("delivery-1", true, cancellation.Token));
    }

    [Fact]
    public async Task 경로대기중_소유계정이바뀌면_개인지도를반환하지않는다()
    {
        await using var f = await Fixture.Create();
        f.Directions.OnLookup = () => f.Current.UserId = "other-owner";
        Assert.Null(await f.Service.내지도Async("delivery-1", true));
    }

    [Fact]
    public async Task 경로대기중_원장좌표가바뀌면_이전경로를제거한다()
    {
        await using var f = await Fixture.Create();
        f.Directions.OnLookup = () => { f.Request.픽업_위도 = 37.55m; f.Db.SaveChanges(); };
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Equal(37.55m, map!.Pickup!.Latitude);
        Assert.Null(map.PlannedRoute);
        Assert.Equal(NeighborhoodDeliveryMapStates.Unavailable, map.RouteStateCode);
    }

    [Fact]
    public async Task 현재배정이후최근GPS는_지도와기존본인상세에만표시된다()
    {
        await using var f = await Fixture.Create();
        await f.AddLocation(f.Clock.Now.AddMinutes(-1), f.Clock.Now.AddSeconds(-30));
        var map = await f.Service.내지도Async("delivery-1");
        Assert.Equal(NeighborhoodDeliveryMapStates.Available, map!.DriverLocationStateCode);
        Assert.Equal(37.51m, map.DriverLocation!.Latitude);
        Assert.Equal(f.Clock.Now.AddSeconds(-30), map.DriverLocation.ReceivedAtUtc);
        var detail = await f.Deliveries.내상세Async("delivery-1");
        Assert.Equal(37.51m, detail!.Request.기사최근위도);
    }

    [Fact]
    public async Task 외부모의처리처의지도는기존기사GPS를사용하지않고픽업전달참고경로만조회한다()
    {
        await using var f = await Fixture.Create();
        f.Deliveries.Fulfillment = new() { RequestId = "delivery-1", ProviderId = "mock-local",
            StateCode = NeighborhoodFulfillmentStates.Accepted, Revision = 2, IsSimulation = true };
        await f.AddLocation(f.Clock.Now.AddMinutes(-1), f.Clock.Now.AddSeconds(-30));
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Null(map!.DriverLocation);
        Assert.Equal(NeighborhoodDeliveryMapStates.NoRecentLocation, map.DriverLocationStateCode);
        Assert.Contains("모의 처리처", map.DriverLocationNotice);
        Assert.Equal(NeighborhoodDeliveryMapStages.Overview, map.StageCode);
        Assert.Equal("mock-local", map.Fulfillment!.ProviderId);
        Assert.Equal("Pickup", map.PlannedRoute!.FromPointRoleCode);
        Assert.Equal((37.5m, 127m, 37.6m, 127.1m), f.Directions.LastInput);
    }

    [Theory]
    [InlineData(NeighborhoodFulfillmentStates.Completed, false)]
    [InlineData(NeighborhoodFulfillmentStates.Cancelled, false)]
    [InlineData(NeighborhoodFulfillmentStates.Completed, true)]
    [InlineData(NeighborhoodFulfillmentStates.Cancelled, true)]
    public async Task 모의수행이종료되면경로를제거하고외부조회중바뀐최신상태도반영한다(string state, bool duringLookup)
    {
        await using var f = await Fixture.Create();
        f.Deliveries.Fulfillment = new() { RequestId = "delivery-1", ProviderId = "mock-local", Revision = 2,
            StateCode = duringLookup ? NeighborhoodFulfillmentStates.Accepted : state, IsSimulation = true };
        if (duringLookup) f.Directions.OnLookup = () => f.Deliveries.Fulfillment = new()
            { RequestId = "delivery-1", ProviderId = "mock-local", Revision = 3, StateCode = state, IsSimulation = true };
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Equal(state, map!.Fulfillment!.StateCode);
        Assert.Equal(NeighborhoodDeliveryMapStages.Closed, map.StageCode);
        Assert.Equal(NeighborhoodDeliveryMapStates.Closed, map.RouteStateCode);
        Assert.Null(map.PlannedRoute); Assert.Null(map.DriverLocation);
        Assert.Equal(duringLookup ? 1 : 0, f.Directions.Calls);
    }

    [Fact]
    public async Task 원의뢰가취소됐으면모의처리처의이전접수상태가지도진행을다시열지않는다()
    {
        await using var f = await Fixture.Create();
        f.Request.상태 = 상태값.의뢰상태.취소; f.Request.배차상태 = 상태값.배차상태.취소;
        await f.Db.SaveChangesAsync();
        f.Deliveries.Fulfillment = new() { RequestId = "delivery-1", ProviderId = "mock-local",
            StateCode = NeighborhoodFulfillmentStates.Accepted, Revision = 2, IsSimulation = true };
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Equal(상태값.의뢰상태.취소, map!.RequestStatusCode);
        Assert.Equal(NeighborhoodDeliveryMapStages.Closed, map.StageCode);
        Assert.Equal(NeighborhoodDeliveryMapStates.Closed, map.RouteStateCode);
        Assert.Null(map.PlannedRoute); Assert.Null(map.DriverLocation); Assert.Equal(0, f.Directions.Calls);
    }

    [Theory]
    [InlineData("배차확정", "Pickup", "Pickup")]
    [InlineData("상차지도착", "Pickup", "Pickup")]
    [InlineData("상차완료", "Delivery", "Dropoff")]
    [InlineData("운송중", "Delivery", "Dropoff")]
    public async Task fresh기사위치가있으면_현재수행단계의목적지까지_예정경로만조회한다(string status, string stage, string goal)
    {
        await using var f = await Fixture.Create();
        f.Queue.상태 = status;
        await f.AddLocation(f.Clock.Now.AddMinutes(-1), f.Clock.Now.AddSeconds(-30));
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Equal(stage, map!.StageCode);
        Assert.Equal(stage, map.PlannedRoute!.StageCode);
        Assert.Equal("Driver", map.PlannedRoute.FromPointRoleCode);
        Assert.Equal(goal, map.PlannedRoute.ToPointRoleCode);
        Assert.Equal(37.51m, f.Directions.LastInput.Item1);
        Assert.Equal(goal == "Pickup" ? 37.5m : 37.6m, f.Directions.LastInput.Item3);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reassign")]
    [InlineData("next-stage")]
    public async Task 외부경로대기중_종료나기사나단계가바뀌면_이전진행경로를버린다(string change)
    {
        await using var f = await Fixture.Create();
        f.Queue.상태 = "배차확정";
        await f.AddLocation(f.Clock.Now.AddMinutes(-1), f.Clock.Now.AddSeconds(-30));
        f.Directions.OnLookup = () =>
        {
            if (change == "cancel") f.Request.배차상태 = 상태값.배차상태.취소;
            if (change == "reassign") f.Queue.확정기사Id = "driver-2";
            if (change == "next-stage") f.Queue.상태 = "상차완료";
            f.Db.SaveChanges();
        };
        var map = await f.Service.내지도Async("delivery-1", true);
        Assert.Null(map!.PlannedRoute);
        if (change != "next-stage") Assert.Null(map.DriverLocation);
    }

    [Theory]
    [InlineData("stale-measured")]
    [InlineData("stale-received")]
    [InlineData("before-assignment")]
    [InlineData("future-measured")]
    [InlineData("future-received")]
    [InlineData("missing-assignment")]
    [InlineData("reassigned")]
    [InlineData("round-changed")]
    [InlineData("closed")]
    public async Task 유효하지않은GPS는_지도뿐아니라기존본인상세에서도제거한다(string invalid)
    {
        await using var f = await Fixture.Create();
        var measured = f.Clock.Now.AddMinutes(-1); var received = measured.AddSeconds(10);
        switch (invalid)
        {
            case "stale-measured": measured = f.Clock.Now.AddMinutes(-11); break;
            case "stale-received": received = f.Clock.Now.AddMinutes(-11); break;
            case "before-assignment": measured = f.Clock.Now.AddMinutes(-4); break;
            case "future-measured": measured = f.Clock.Now.AddHours(1); break;
            case "future-received": received = f.Clock.Now.AddHours(1); break;
            case "missing-assignment": f.Db.운송이벤트.RemoveRange(f.Db.운송이벤트); break;
            case "reassigned": f.Queue.확정기사Id = "driver-2"; break;
            case "round-changed": ++f.Queue.추천라운드; break;
            case "closed": f.Request.배차상태 = 상태값.배차상태.취소; break;
        }
        await f.AddLocation(measured, received);
        var map = await f.Service.내지도Async("delivery-1");
        Assert.NotNull(map);
        Assert.Null(map.DriverLocation);
        var detail = await f.Deliveries.내상세Async("delivery-1");
        Assert.Null(detail!.Request.기사최근위도);
        Assert.Null(detail.Request.기사최근경도);
        Assert.Null(detail.Request.기사최근위치시각Utc);
    }

    [Theory]
    [InlineData(-2, true)]
    [InlineData(-60, false)]
    public async Task 일반화물은_생활배송전용배정증거없이_현재수락후최근10분GPS만반환한다(int measuredMinutesAgo, bool visible)
    {
        await using var f = await Fixture.Create();
        f.Request.클라이언트요청Id = "legacy:1";
        // 일반화물은 생활배송 전용 배정 사건을 요구하지 않는다. 실제 화물 수락의 기존 근거를 사용한다.
        f.Db.운송이벤트.RemoveRange(f.Db.운송이벤트);
        var measured = f.Clock.Now.AddMinutes(measuredMinutesAgo);
        f.Db.운송이벤트.Add(CargoAcceptanceRecoveryMarker.Create(f.Queue, measured.AddMinutes(-1)));
        await f.AddLocation(measured, measured.AddSeconds(10));
        var detail = await f.Deliveries.내상세Async("delivery-1");
        Assert.NotNull(detail);
        if (visible) Assert.Equal(37.51m, detail.Request.기사최근위도);
        else
        {
            Assert.Null(detail.Request.기사최근위도);
            Assert.Null(detail.Request.기사최근경도);
            Assert.Null(detail.Request.기사최근위치시각Utc);
        }
        Assert.Null(await f.Service.내지도Async("delivery-1")); // 생활배송 전용 지도에는 여전히 노출하지 않는다.
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("neighborhood-map-" + Guid.NewGuid()).Options, new Encryption());
        public Current Current { get; } = new();
        public Clock Clock { get; } = new();
        public Directions Directions { get; } = new();
        public 화주운송의뢰 Request { get; private set; } = null!;
        public 운송원장 Queue { get; private set; } = null!;
        public Deliveries Deliveries { get; private set; } = null!;
        public 생활배송지도Service Service => new(Db, Current, Deliveries, Directions, Clock, NullLogger<생활배송지도Service>.Instance);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            f.Request = new()
            {
                의뢰Id = "delivery-1", 주문자UserId = "owner-1", 화주Id = "owner-1",
                클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "fixture",
                상태 = 상태값.의뢰상태.생성됨, 배차상태 = 상태값.배차상태.배차확정,
                픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.6m, 하차_경도 = 127.1m,
                픽업_도로명주소 = "비공개 상세 주소", 픽업_연락처_이름 = "비공개 담당자", 픽업_연락처_전화번호 = "010-1234-5678"
            };
            f.Queue = new()
            {
                의뢰Id = "delivery-1", 운송번호 = "delivery-1", 확정기사Id = "driver-1", 기사_운송자 = "driver-1",
                배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.확정,
                추천라운드 = 3, 상태 = 상태값.배차상태.운송중
            };
            f.Db.화주운송의뢰.Add(f.Request); f.Db.운송원장.Add(f.Queue);
            f.Db.운송이벤트.Add(NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(f.Request, f.Queue, f.Clock.Now.AddMinutes(-3))!);
            await f.Db.SaveChangesAsync();
            f.Deliveries = new(f.Db, f.Current, f.Clock);
            return f;
        }
        public async Task AddLocation(DateTime measured, DateTime received)
        {
            Db.기사위치기록.Add(new 기사위치기록
            {
                기사Id = Queue.확정기사Id!, 위도 = 37.51m, 경도 = 127.01m, 정확도_m = 10,
                기록시각 = measured, CreatedAt = received, UpdatedAt = received
            });
            await Db.SaveChangesAsync();
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class Deliveries(SsalddelContext db, Current current, Clock clock) : I생활배송의뢰UseCase
    {
        public bool Deny { get; set; }
        public NeighborhoodFulfillmentSnapshot? Fulfillment { get; set; }
        public async Task<NeighborhoodDeliveryResponse?> 내상세Async(string id, CancellationToken cancellationToken = default)
        {
            if (Deny) return null;
            var result = await new 의뢰단건조회QueryHandler(db, new 화주운송업무담당자UseCase(db, current, clock))
                .Handle(new 의뢰단건조회Query(id), cancellationToken);
            return result is null ? null : new NeighborhoodDeliveryResponse { RequestId = id, Request = result, Fulfillment = Fulfillment };
        }
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> 내목록Async(int page = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NeighborhoodDeliveryQuoteResponse>> 견적Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NeighborhoodDeliveryResponse>> 등록Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId { get; set; } = "owner-1";
        public string? Role => "일반회원";
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; } = DateTime.UtcNow;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Directions : INaverCloudDirectionsService
    {
        public int Calls { get; private set; }
        public string? Failure { get; set; }
        public Action? OnLookup { get; set; }
        public (decimal, decimal, decimal, decimal) LastInput { get; private set; }
        public Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(decimal startLat, decimal startLng, decimal goalLat, decimal goalLng,
            string? option = null, CancellationToken cancellationToken = default)
        {
            ++Calls; LastInput = (startLat, startLng, goalLat, goalLng); OnLookup?.Invoke();
            if (Failure == "exception") throw new HttpRequestException("private-provider-key");
            if (Failure == "unavailable") return Task.FromResult<NaverCloudDrivingRoute?>(null);
            return Task.FromResult<NaverCloudDrivingRoute?>(new()
            {
                DistanceMeters = 2400m, DurationMilliseconds = 360000m,
                Path = Failure == "no-path" ? [] : Failure == "invalid-path" ? [new(37.5m, 127m), new(999m, 127m)]
                    : [new(startLat, startLng), new(37.55m, 127.05m), new(goalLat, goalLng)]
            });
        }
        public Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(decimal startLat, decimal startLng, decimal goalLat, decimal goalLng,
            IReadOnlyList<NaverCloudRouteWaypoint> waypoints, string? option = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
