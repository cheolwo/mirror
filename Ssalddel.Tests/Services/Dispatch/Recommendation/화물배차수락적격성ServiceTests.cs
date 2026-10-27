using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Services.Dispatch.Coordination;
using System.Text.Json;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 화물배차수락적격성ServiceTests
{
    [Fact]
    public async Task 진행중운송이있는데_차량제원이없으면_추가수락을차단한다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 차량 = "등록되지않은차량" });
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "active-1",
            운송번호 = "active-1",
            기사_운송자 = "driver-1",
            배차큐단계 = 상태값.배차큐단계.확정,
            상태 = "상차완료"
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).평가Async(
            "driver-1",
            Candidate("candidate-1"),
            [],
            CancellationToken.None);

        Assert.False(result.수락가능);
        Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    [Fact]
    public async Task 첫단독운송도_차량제원이없으면_경고확인으로수락할수없다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 차량 = "등록되지않은차량" });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = Candidate("candidate-1");

        var withoutAcknowledgement = await service.평가Async("driver-1", request, [], CancellationToken.None);
        var acknowledged = await service.평가Async(
            "driver-1",
            request,
            [화물배차수락경고코드.차량주의사항],
            CancellationToken.None);

        Assert.False(withoutAcknowledgement.수락가능);
        Assert.Equal(화물배차수락오류코드.차량적합실패, withoutAcknowledgement.오류코드);
        Assert.False(acknowledged.수락가능);
        Assert.Equal(화물배차수락오류코드.차량적합실패, acknowledged.오류코드);
        Assert.Contains("검증 불가", acknowledged.오류메시지);
    }

    [Fact]
    public async Task 첫단독도_현재위치와실제경로가확인되어야하며_구형작업시간한계를남긴다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new() { 기사Id = "driver-1", 차량 = "fixture" });
        db.차량제원.Add(new() { 차량코드 = "fixture", 적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 100, 팔레트적재개수 = 1 });
        await db.SaveChangesAsync();
        var request = ReadyCandidate();
        화물배차수락적격성Service Service(bool actual) => new(db, new StubLocationStore(true),
            new 차량화물적합성Service(new 차량적재추천Engine()), new 기사운송일정구성Service(db), new 운송일정삽입평가Service(new StubRouteService(actual)));
        Assert.False((await Service(false).평가Async("driver-1", request, [])).수락가능);
        var accepted = await Service(true).평가Async("driver-1", request, []);
        Assert.True(accepted.수락가능); Assert.Equal(2, accepted.권장경로순서.Count); Assert.Single(accepted.검증한계);
    }

    [Fact]
    public async Task 추천에기록된상하차시간을_수락때빠뜨려약속을통과시키지않는다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new() { 기사Id = "driver-1", 차량 = "fixture" });
        db.차량제원.Add(new() { 차량코드 = "fixture", 적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 100, 팔레트적재개수 = 1 });
        var request = ReadyCandidate(); request.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(10);
        db.운송원장.Add(new() { 의뢰Id = request.의뢰Id, 운송번호 = request.의뢰Id,
            추천판정근거Json = JsonSerializer.Serialize(new 국내화물추천저장근거 { 수행계획 = new() {
                상하차작업시간 = new Dictionary<string, 화물상하차작업시간> { [request.의뢰Id] = new(20, 20) } } }) });
        await db.SaveChangesAsync();
        var service = new 화물배차수락적격성Service(db, new StubLocationStore(true), new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(db), new 운송일정삽입평가Service(new StubRouteService(true)));
        var result = await service.평가Async("driver-1", request, []);
        Assert.False(result.수락가능); Assert.Equal(화물배차수락오류코드.일정실패, result.오류코드);
    }

    [Fact]
    public async Task 추천저장전에도_명시한상하차시간으로마감초과를차단한다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new() { 기사Id = "driver-1", 차량 = "fixture" });
        db.차량제원.Add(new() { 차량코드 = "fixture", 적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 100, 팔레트적재개수 = 1 });
        await db.SaveChangesAsync();
        var request = ReadyCandidate(); request.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(10);
        var service = new 화물배차수락적격성Service(db, new StubLocationStore(true), new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(db), new 운송일정삽입평가Service(new StubRouteService(true)));
        Assert.True((await service.평가Async("driver-1", request, [], default)).수락가능);
        var explicitTimes = new Dictionary<string, 화물상하차작업시간> { [request.의뢰Id] = new(20, 20) };
        var result = await service.평가Async("driver-1", request, [], explicitTimes, default);
        Assert.False(result.수락가능); Assert.Equal(화물배차수락오류코드.일정실패, result.오류코드);
        Assert.Empty(await db.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 구형적격성구현은_명시작업시간검증을조용히생략할수없다()
    {
        I화물배차수락적격성Service service = new LegacyEligibility();
        var result = await service.평가Async("driver-1", ReadyCandidate(), [],
            new Dictionary<string, 화물상하차작업시간> { ["candidate"] = new(20, 20) }, default);
        Assert.False(result.수락가능); Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    [Fact]
    public async Task 확정기사만기록된진행업무도_기존약속초과를놓치지않는다()
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        var active = ReadyCandidate(); active.의뢰Id = "active"; active.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(1);
        db.화주운송의뢰.Add(active);
        db.운송원장.Add(new() { 의뢰Id = active.의뢰Id, 운송번호 = active.의뢰Id, 확정기사Id = "driver-1",
            배차큐단계 = 상태값.배차큐단계.확정, 상태 = "상차완료" });
        await db.SaveChangesAsync();
        var plan = await new 기사운송일정구성Service(db).구성Async("driver-1", new(37m, 127m));
        Assert.Equal("active", Assert.Single(plan.항목목록).의뢰Id);
        var result = await ReadyService(db).평가Async("driver-1", ReadyCandidate(), []);
        Assert.False(result.수락가능); Assert.Equal(화물배차수락오류코드.일정실패, result.오류코드);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 취소종료기록과배차해제기록은_새운송일정으로다시수행하지않는다(bool released)
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        var previous = new 운송원장 { 의뢰Id = "closed", 운송번호 = "closed", 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            상태 = 상태값.배차상태.취소, 배차큐단계 = 상태값.배차큐단계.종료 };
        if (released) 배차재추천상태Policy.적용(previous, "driver-1", DateTime.UtcNow);
        db.운송원장.Add(previous); await db.SaveChangesAsync();
        Assert.Empty((await new 기사운송일정구성Service(db).구성Async("driver-1", new(37m, 127m))).항목목록);
        Assert.True((await ReadyService(db).평가Async("driver-1", ReadyCandidate(), [])).수락가능);
    }

    [Fact]
    public async Task 큐가종료돼도_반납잔량책임은기존조회로차단하고_반납후에만재개한다()
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        db.운송원장.Add(new() { 의뢰Id = "return", 운송번호 = "return", 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            상태 = "반환대기", 배차큐단계 = 상태값.배차큐단계.종료 });
        db.Set<분산배송WaveRecord>().Add(new() { WaveId = "wave", DriverId = "driver-1" });
        db.Set<분산배송주문Record>().Add(new() { OrderId = "order", WaveId = "wave", CargoRequestId = "return", StatusCode = "ReturnRequired" });
        var reservation = new 분산재고예약Record { ReservationId = "reservation", WaveId = "wave", OrderId = "order", Quantity = 1, PickedQuantity = 1 };
        db.Set<분산재고예약Record>().Add(reservation); await db.SaveChangesAsync();
        var service = ReadyService(db);
        var held = await service.평가Async("driver-1", ReadyCandidate(), []);
        Assert.False(held.수락가능); Assert.Equal("DriverWork.ReturnCustodyPending", held.오류코드);
        reservation.ReturnedQuantity = 1; await db.SaveChangesAsync();
        Assert.True((await service.평가Async("driver-1", ReadyCandidate(), [])).수락가능);
    }

    [Fact]
    public async Task 운송번호가다른표시값이어도_의뢰Id로기존원장과일정을연결한다()
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        var active = ReadyCandidate(); active.의뢰Id = "active"; db.화주운송의뢰.Add(active);
        db.운송원장.Add(new() { 의뢰Id = active.의뢰Id, 운송번호 = "legacy-transport-number", 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            배차큐단계 = 상태값.배차큐단계.확정, 상태 = "상차완료" });
        await db.SaveChangesAsync();
        var plan = await new 기사운송일정구성Service(db).구성Async("driver-1", new(37m, 127m));
        var stop = Assert.Single(plan.항목목록); Assert.Equal(active.의뢰Id, stop.의뢰Id); Assert.NotNull(stop.좌표);
        var result = await ReadyService(db).평가Async("driver-1", ReadyCandidate(), []);
        Assert.True(result.수락가능); Assert.Contains(result.권장경로순서, x => x.EndsWith(" active", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 정본의뢰키가비었거나기사귀속이모호하면_운송번호로대신확정하지않는다(bool emptyId)
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        db.운송원장.Add(new() { 의뢰Id = emptyId ? "" : "active", 운송번호 = "active", 확정기사Id = "driver-1",
            기사_운송자 = emptyId ? "driver-1" : "other-driver", 배차큐단계 = 상태값.배차큐단계.확정, 상태 = "상차완료" });
        await db.SaveChangesAsync();
        var result = await ReadyService(db).평가Async("driver-1", ReadyCandidate(), []);
        Assert.False(result.수락가능); Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    [Fact]
    public async Task 근거없는하루작업시간상한을만들지않고_실제완료약속으로판정한다()
    {
        await using var db = CreateContext(); await SeedReadyDriverAsync(db);
        var candidate = ReadyCandidate(); candidate.픽업_시간창_종료일시 = DateTime.UtcNow.AddDays(3); candidate.하차_시간창_종료일시 = DateTime.UtcNow.AddDays(4);
        var result = await ReadyService(db).평가Async("driver-1", candidate, [],
            new Dictionary<string, 화물상하차작업시간> { [candidate.의뢰Id] = new(1500, 10) }, default);
        Assert.True(result.수락가능); Assert.True(result.총소요시간분 > 1440m);
    }

    private static async Task SeedReadyDriverAsync(SsalddelContext db)
    {
        db.용달기사.Add(new() { 기사Id = "driver-1", 차량 = "fixture" });
        db.차량제원.Add(new() { 차량코드 = "fixture", 적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 100, 팔레트적재개수 = 1 });
        await db.SaveChangesAsync();
    }

    private static 화물배차수락적격성Service ReadyService(SsalddelContext db) => new(db, new StubLocationStore(true),
        new 차량화물적합성Service(new 차량적재추천Engine()), new 기사운송일정구성Service(db), new 운송일정삽입평가Service(new StubRouteService(true)));

    private static 화주운송의뢰 ReadyCandidate() => new()
    {
        의뢰Id = "candidate", 화물종류 = "합성 상자", 화물수량 = 1, 화물중량Kg = 10, 화물입력부피Cbm = .1m, 화물부피Cbm = .1m,
        화물부피근거Code = "UserProvided", 화물적재계산판본 = 화물적재입력Policy.CurrentRevision,
        화물바닥회전가능여부 = true, 화물적층가능여부 = true, 화물팔레트개수 = 0, 화물온도조건 = "상온",
        픽업_위도 = 37m, 픽업_경도 = 127m, 하차_위도 = 37.01m, 하차_경도 = 127.01m,
        픽업_시간창_시작일시 = DateTime.UtcNow.AddMinutes(-10), 픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(1),
        하차_시간창_종료일시 = DateTime.UtcNow.AddHours(2)
    };

    private static 화물배차수락적격성Service CreateService(SsalddelContext db)
    {
        var route = new StubRouteService();
        return new 화물배차수락적격성Service(
            db,
            new StubLocationStore(),
            new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(db),
            new 운송일정삽입평가Service(route));
    }

    private static 화주운송의뢰 Candidate(string id) => new()
    {
        의뢰Id = id,
        운송방식 = "혼적",
        화물종류 = "상자",
        픽업_도로명주소 = "상차지",
        하차_도로명주소 = "하차지"
    };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"freight-acceptance-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class StubLocationStore(bool available = false) : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot snapshot) { }
        public bool TryGetLatest(string driverId, out DriverLocationSnapshot snapshot)
        {
            snapshot = available ? new(driverId, 37m, 127m, 1m, "운행중", DateTime.UtcNow, DateTime.UtcNow) : null!;
            return available;
        }
    }

    private sealed class StubRouteService(bool actual = false) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Task.FromResult<배차경로예상결과?>(actual
            ? new(1m, TimeSpan.FromMinutes(2), null, "합성 실제경로 응답", true) { RouteOption = "fixture-car" } : null);
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => null;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class LegacyEligibility : I화물배차수락적격성Service
    {
        public Task<화물배차수락적격성결과> 평가Async(string 기사Id, 화주운송의뢰 후보의뢰,
            IReadOnlyCollection<string> 확인한경고코드, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물배차수락적격성결과(true, null, null, [], [], [], null, null));
    }
}
