using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Coordination;

/// <summary>실제 일정 삽입·구간 적재 검사와 격리 DB를 실행한다. 경로와 실차 자료만 명시적인 합성 대역이다.</summary>
public sealed class 국내화물배차계획검증Tests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    private static readonly DateTime Now = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 두건을함께검증하고같은계획Id와확정보수에연결한다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Run();
        Assert.Equal(2, result.추천배정목록.Count);
        var plan = Assert.Single(result.검증계획목록.Where(x => x.가능여부));
        Assert.All(result.추천배정목록, x => Assert.Equal(plan.계획Id, x.수행계획Id));
        Assert.True(plan.구간검증결과!.확인됨);
        Assert.Equal(20000m, plan.합의기사보수합계);
        Assert.Equal(4, plan.경로순서.Count);
        Assert.All(plan.경로순서, x => Assert.True(x.출발시각Utc >= x.도착시각Utc));
        Assert.False(plan.계획Id.Contains("고객", StringComparison.Ordinal));
        Assert.DoesNotContain("비공개 고객 주소", System.Text.Json.JsonSerializer.Serialize(plan));
    }

    [Fact]
    public async Task 개별적으로가능해도두번째가약속을넘으면단건만남긴다()
    {
        await using var f = await Fixture.Create(deadlineMinutes: 12);
        var result = await f.Run();
        Assert.Single(result.추천배정목록);
        Assert.Contains(result.보류목록, x => x.의뢰Id == "REQ-2" && x.사유 == "CargoPlan.NoVerifiedJointSchedule");
    }

    [Fact]
    public async Task 동시상차가필수인시간창에서중량을넘는조합은제안하지않는다()
    {
        await using var f = await Fixture.Create(weightLimit: 10);
        foreach (var row in await f.Db.화주운송의뢰.ToArrayAsync()) row.픽업_시간창_종료일시 = Now.AddMinutes(12);
        await f.Db.SaveChangesAsync();
        var result = await f.Run();
        Assert.Single(result.추천배정목록);
        Assert.All(result.검증계획목록.Where(x => x.가능여부).SelectMany(x => x.구간검증결과!.구간목록),
            x => Assert.True(x.총중량Kg <= 10));
    }

    [Fact]
    public async Task 작업시간을누락하면확인된영분으로바꾸지않는다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Run(times: false);
        Assert.Empty(result.추천배정목록);
        Assert.All(result.검증계획목록, x => Assert.False(x.확인됨));
        Assert.All(result.보류목록, x => Assert.Contains("HandlingTimeMissing", x.사유));
    }

    [Fact]
    public async Task 도착은제시간이어도작업완료가늦으면차단한다()
    {
        await using var f = await Fixture.Create(deadlineMinutes: 12);
        var result = await f.Run(serviceMinutes: 4);
        Assert.Empty(result.추천배정목록);
        Assert.All(result.보류목록, x => Assert.Equal("CargoPlan.NoVerifiedJointSchedule", x.사유));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 실차또는입력근거가미확인이면가능한계획으로승격하지않는다(bool actualVehicle, bool basis)
    {
        await using var f = await Fixture.Create();
        f.Vehicle.Confirmed = actualVehicle;
        var result = await f.Run(basis: basis);
        Assert.Empty(result.추천배정목록);
        Assert.All(result.검증계획목록, x => Assert.False(x.가능여부));
    }

    [Fact]
    public async Task 근사경로만있으면실제경로통과로표시하지않는다()
    {
        await using var f = await Fixture.Create();
        f.Routes.Verified = false;
        Assert.Empty((await f.Run()).추천배정목록);
    }

    [Fact]
    public async Task 비용이누락된제안의합계를영원으로표시하지않는다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Run(unknownCost: true);
        Assert.Equal(2, result.추천배정목록.Count);
        Assert.Null(result.전체예상비용);
        Assert.Null(result.전체예상순이익);
    }

    [Fact]
    public async Task 확정된기사보수가없으면청구운임으로대체하지않는다()
    {
        await using var f = await Fixture.Create();
        foreach (var fare in await f.Db.운임구성.ToArrayAsync()) fare.기사지급예정운임 = null;
        await f.Db.SaveChangesAsync();
        var result = await f.Run();
        Assert.Empty(result.추천배정목록);
        Assert.All(result.보류목록, x => Assert.Equal("CargoPlan.PayoutOrDecisionEvidenceMissing", x.사유));
    }

    [Fact]
    public async Task 준비대기와작업시간을전체소요시간에포함한다()
    {
        var service = new 운송일정삽입평가Service(new Routes());
        var request = Request("one", 60);
        request.픽업_시간창_시작일시 = Now.AddMinutes(20);
        var plan = new 기사운송일정계획("driver", Now, new(37, 127), [])
        { 상하차작업시간 = new Dictionary<string, 화물상하차작업시간> { ["one"] = new(2, 3) } };
        var result = await service.평가Async(plan, request);
        Assert.True(result.전체완수가능여부);
        Assert.Equal(30m, result.총소요시간분); // 이동10 + 준비대기15 + 작업5
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("rotation")]
    [InlineData("stacking")]
    public async Task 구형입력의호환가정을확인된전체계획으로승격하지않는다(string missing)
    {
        await using var f = await Fixture.Create();
        foreach (var row in await f.Db.화주운송의뢰.ToArrayAsync())
        {
            if (missing == "quantity") row.화물수량 = null;
            if (missing == "rotation") row.화물바닥회전가능여부 = null;
            if (missing == "stacking") row.화물적층가능여부 = null;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.Run();
        Assert.Empty(result.추천배정목록);
        Assert.All(result.보류목록, x => Assert.Equal("CargoPlan.LoadingAssumptionsUnverified", x.사유));
        Assert.All(result.검증계획목록, x => Assert.False(x.확인됨));
    }

    [Fact]
    public async Task 참여관문미실행은자료지문이있어도전체계획확인이아니다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Run(participationUnverified: true);
        Assert.Empty(result.추천배정목록);
        Assert.All(result.보류목록, x => Assert.Equal("CargoPlan.ParticipationEvidenceMissing", x.사유));
        Assert.All(result.검증계획목록, x => Assert.False(x.확인됨));
    }

    private static 화주운송의뢰 Request(string id, int deadline) => new()
    {
        의뢰Id = id, 화주Id = "synthetic-shipper", 화물종류 = "상온 상자", 화물설명 = "합성 물품",
        화물수량 = 1, 화물중량Kg = 6m, 화물부피Cbm = 1m, 화물입력부피Cbm = 1m,
        화물부피근거Code = "UserProvided", 화물적재계산판본 = 화물적재입력Policy.CurrentRevision,
        화물길이Mm = 1000, 화물폭Mm = 1000, 화물높이Mm = 1000,
        화물바닥회전가능여부 = true, 화물적층가능여부 = true, 화물팔레트개수 = 0, 화물온도조건 = "상온",
        픽업_도로명주소 = "합성 공개도로", 하차_도로명주소 = "비공개 고객 주소를 계획 요약에 복제하지 않음",
        픽업_위도 = 37.01m, 픽업_경도 = 127.01m, 하차_위도 = 37.02m, 하차_경도 = 127.02m,
        픽업_시간창_시작일시 = Now, 픽업_시간창_종료일시 = Now.AddMinutes(deadline),
        하차_시간창_시작일시 = Now, 하차_시간창_종료일시 = Now.AddMinutes(deadline), 최종운임 = 99999m
    };

    private sealed class Fixture(SsalddelContext db, Vehicles vehicle, Routes routes) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Vehicles Vehicle { get; } = vehicle;
        public Routes Routes { get; } = routes;
        public static async Task<Fixture> Create(int deadlineMinutes = 60, int weightLimit = 100)
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase("cargo-plan-r76-" + Guid.NewGuid(), Root).Options, new Encryption());
            foreach (var (id, n) in new[] { ("REQ-1", 1L), ("REQ-2", 2L) })
            {
                var request = Request(id, deadlineMinutes); request.운임구성Id = n; db.화주운송의뢰.Add(request);
                db.운임구성.Add(new() { Id = n, 의뢰Id = id, 기사지급예정운임 = 10000m, 최종운임 = 99999m });
            }
            await db.SaveChangesAsync();
            return new(db, new(weightLimit), new());
        }
        public Task<국내화물배차조율결과> Run(bool times = true, decimal serviceMinutes = 0,
            bool basis = true, bool unknownCost = false, bool participationUnverified = false)
        {
            var evidence = new 국내화물배차판정근거
            {
                기준시각Utc = Now, 입력지문 = "explicit-synthetic-input-r76",
                미확인사유 = participationUnverified ? ["DispatchEvidence.ParticipationGuardDisabled"] : []
            };
            var ids = new[] { "REQ-1", "REQ-2" };
            var input = new 국내화물배차조율입력(Now, 2,
                ids.Select(id => new 운송의뢰조율입력(1, id, "CargoTransport", "상자", "상온", 6, 99999,
                    "synthetic", "합성", new(37.01m, 127.01m), new(37.02m, 127.02m), Now, Now.AddHours(1), Now, Now.AddHours(1), 0, Now)).ToArray(),
                [new("driver", "synthetic", "운행중", 0, "synthetic", "합성", new(37, 127), 0, Now, 50, Now)], [])
            {
                판정근거 = basis ? evidence : null,
                상하차작업시간 = times ? ids.ToDictionary(x => x, _ => new 화물상하차작업시간(serviceMinutes, serviceMinutes)) : null
            };
            var proposals = ids.Select((id, i) => new 국내화물배차제안(i + 1, id, "driver", i + 1, 100,
                unknownCost && i == 1 ? null : 1000m, 99999m, unknownCost && i == 1 ? null : 98999m, "합성 비교", []) { 판정근거 = evidence }).ToArray();
            var result = new 국내화물배차조율결과(Now, proposals, [], [], 2000, 199998, 197998) { 판정근거 = input.판정근거 };
            return new 국내화물배차계획검증Service(Db, new EmptySchedule(), new 운송일정삽입평가Service(Routes), Vehicle,
                new 화물구간적재검증Service(new 차량적재추천Engine())).검증Async(input, result);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class Vehicles(int weight) : I기사실차적재근거조회Service
    {
        public bool Confirmed { get; set; } = true;
        public Task<기사실차적재조회결과> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 기사실차적재조회결과
        {
            실차확인됨 = Confirmed, 판본 = 1, 근거지문 = "synthetic-measurement", 사유Code = "SyntheticFixture",
            유효제원 = new 차량제원 { 차량코드 = "synthetic", 차량명 = "합성 실측 대역", 적재함길이Mm = 2000, 적재함폭Mm = 2000,
                적재함높이Mm = 2000, 최대적재중량Kg = weight, 운영권장중량Kg = weight, 권장최대CBM = 4, 팔레트적재개수 = 2 }
        });
    }
    private sealed class EmptySchedule : I기사운송일정구성Service
    {
        public Task<기사운송일정계획> 구성Async(string id, 배차경로좌표? origin, CancellationToken ct = default)
            => Task.FromResult(new 기사운송일정계획(id, Now, origin, []));
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Routes : I배차추천경로Service
    {
        public bool Verified { get; set; } = true;
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? from, 배차경로좌표? to)
            => Task.FromResult<배차경로예상결과?>(new(1, TimeSpan.FromMinutes(5), 0, "synthetic-provider", Verified) { RouteOption = "synthetic-car" });
        public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => 1m;
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? loc, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(new(37, 127));
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? loc) => Task.FromResult<배차경로좌표?>(new(37, 127));
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => EstimateRouteAsync(origin, stops.Last());
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? a, 배차경로좌표? b, 배차경로좌표? c, 배차경로좌표? d) => Task.FromResult<배차삽입경로예상결과?>(null);
    }
}
