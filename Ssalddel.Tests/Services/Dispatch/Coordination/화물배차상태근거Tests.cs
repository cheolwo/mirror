using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.차량;

namespace Ssalddel.Tests.Application.Food;

// 기존 격리 SQLite fixture를 재사용한다. 서버·GPS·모델 또는 외부 알림을 실행하지 않는다.
public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task R76_동일상태재조회는_관측시각과원자성한계를남기고같은지문을유지한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        var first = await f.Evidence();
        var second = await f.Evidence();
        Assert.Empty(first.차단사유);
        Assert.False(first.원자적조회여부);
        Assert.Equal(f.Now, first.기준시각Utc);
        Assert.Equal(first.입력지문, second.입력지문);
        Assert.True(await f.Snapshots.현재근거확인Async(first));
        Assert.DoesNotContain("격리 시험 픽업지", System.Text.Json.JsonSerializer.Serialize(first), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("payout")]
    [InlineData("selection")]
    [InlineData("location")]
    [InlineData("receiving")]
    [InlineData("actual")]
    [InlineData("qualification")]
    [InlineData("pickupTime")]
    [InlineData("dropoffTime")]
    public async Task R76_판정후원장이나기사조건변경은_이전입력지문을재사용할수없다(string change)
    {
        await using var f = await R76StateFixture.CreateAsync();
        var before = await f.Evidence();
        await f.Change(change);
        Assert.False(await f.Snapshots.현재근거확인Async(before));
    }

    [Fact]
    public async Task R76_오래된위치는_현재위치나완료근거로자동승격하지않는다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        f.States.Current = f.States.Current with { 위치수신시각Utc = f.Now.AddHours(-1) };
        var evidence = await f.Evidence();
        Assert.Contains("DispatchEvidence.LocationUnverified", evidence.차단사유);
        Assert.False(await f.Snapshots.현재근거확인Async(evidence));
    }

    [Fact]
    public async Task R77_동일좌표정확도의신선한heartbeat는_관측만바꾸고안전지문을보존한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        f.States.Current = f.States.Current with
            { 위치기록시각Utc = f.Now.AddSeconds(-2), 위치수신시각Utc = f.Now.AddSeconds(-2) };
        var before = await f.Evidence();
        f.States.Current = f.States.Current with
            { 위치기록시각Utc = f.Now.AddSeconds(-1), 위치수신시각Utc = f.Now.AddSeconds(-1) };
        var after = await f.Evidence();
        Assert.Equal(before.입력지문, after.입력지문);
        Assert.Equal(before.위치관측지문, after.위치관측지문);
        Assert.NotEqual(before.위치수신시각Utc, after.위치수신시각Utc);
        Assert.True(await f.Snapshots.현재근거확인Async(before));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R76_참여보호설정이꺼졌으면_동일원장지문이어도미확인으로보류하고자동활성화하지않는다(bool enabled)
    {
        await using var f = await R76StateFixture.CreateAsync(enabled);
        var queueBefore = System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync());
        var before = await f.Evidence();
        var after = await f.Evidence();

        Assert.Empty(before.차단사유);
        Assert.Equal(before.입력지문, after.입력지문);
        Assert.Equal(enabled, await f.Snapshots.현재근거확인Async(before));
        Assert.Equal(enabled, f.ParticipationOptions.Enabled);
        Assert.Equal(queueBefore, System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync()));
        if (enabled)
            Assert.DoesNotContain("DispatchEvidence.ParticipationGuardDisabled", before.미확인사유);
        else
        {
            Assert.Contains("DispatchEvidence.ParticipationGuardDisabled", before.미확인사유);
            // 저장 근거에 미확인 표시가 누락돼도 현재 조회의 critical 미확인은 통과할 수 없다.
            Assert.False(await f.Snapshots.현재근거확인Async(before with { 미확인사유 = [] }));
        }
    }

    private sealed class R76StateFixture : IAsyncDisposable
    {
        private readonly TestDatabase database;
        public 살뜰.Data.SsalddelContext Db => database.Context;
        public const string Id = "r76-state-request";
        public DateTime Now { get; }
        public R76States States { get; }
        public R76Availability Availability { get; }
        public QualificationFixtureGuard Qualification { get; } = new(Ssalddel.Contracts.Driver.Profile.운송자격Codes.Valid);
        public R76Vehicle Vehicle { get; } = new();
        public SharedDriverWorkOptions ParticipationOptions { get; }
        public 화물배차상태SnapshotService Snapshots { get; }
        private R76StateFixture(TestDatabase database, bool participationEnabled, DateTime? now)
        {
            this.database = database;
            Now = now ?? new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
            States = new(Now);
            Availability = new(Now);
            ParticipationOptions = new() { Enabled = participationEnabled };
            Snapshots = new(Db, States, Vehicle, Availability, Qualification,
                new SharedDriverWorkAdmissionService(Db, Options.Create(ParticipationOptions)),
                new R76Pickup(), Options.Create(new 배차큐정책Options()), new R76Clock(Now));
        }
        public static async Task<R76StateFixture> CreateAsync(bool participationEnabled = true, DateTime? now = null)
        {
            var f = new R76StateFixture(await TestDatabase.CreateAsync(), participationEnabled, now);
            await QualificationSeed(f.Db, Id);
            f.Db.용달기사.Add(new 용달기사 { 기사Id = "driver", 상태 = "활동중", 운행상태 = 상태값.기사운행상태.운행중 });
            f.Db.Set<DriverWorkParticipationRecord>().Add(new() { UserId = "driver", CargoTransport = true,
                MaxConcurrentJobs = 4, CapacityKg = 1000, CapacityLitres = 1000 });
            var fare = new 운임구성 { 의뢰Id = Id, 기사지급예정운임 = 5000, 최종운임 = 6000, UpdatedAt = f.Now };
            f.Db.운임구성.Add(fare); await f.Db.SaveChangesAsync();
            var request = await f.Db.화주운송의뢰.SingleAsync();
            request.운임구성Id = fare.Id; request.화물부피Cbm = .006m;
            request.화물수량 = 1; request.화물바닥회전가능여부 = true; request.화물적층가능여부 = true;
            request.픽업_작업시간분 = 3m; request.하차_작업시간분 = 2m;
            request.픽업_시간창_시작일시 = f.Now.AddHours(1); request.픽업_시간창_종료일시 = f.Now.AddHours(2);
            request.하차_시간창_시작일시 = f.Now.AddHours(2); request.하차_시간창_종료일시 = f.Now.AddHours(3);
            await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            return f;
        }
        public Task<국내화물배차판정근거> Evidence() => Snapshots.조회Async(Id, "driver", Now);
        public async Task Change(string change)
        {
            switch (change)
            {
                case "cargo": (await Db.화주운송의뢰.SingleAsync()).화물수량 = 9; break;
                case "payout": (await Db.운임구성.SingleAsync()).기사지급예정운임 = 7000; break;
                case "selection": (await Db.Set<DriverWorkParticipationRecord>().SingleAsync()).CargoTransport = false; break;
                case "location": States.Current = States.Current with { Latitude = 36.5m }; break;
                case "receiving": Availability.On = false; break;
                case "actual": Vehicle.Revision++; break;
                case "qualification": Qualification.Status = Ssalddel.Contracts.Driver.Profile.운송자격Codes.Suspended; break;
                case "pickupTime": (await Db.화주운송의뢰.SingleAsync()).픽업_작업시간분 = 4m; break;
                case "dropoffTime": (await Db.화주운송의뢰.SingleAsync()).하차_작업시간분 = 3m; break;
            }
            await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
        }
        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    private sealed class R76Clock(DateTime now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class R76Pickup : I화물후속픽업기준Service
    {
        public Task<화물후속픽업기준결과> 조회Async(string id, 배차경로좌표 point, DateTime now, CancellationToken ct = default)
            => Task.FromResult(new 화물후속픽업기준결과(false, true, point, now, null, "ExplicitFixture") { 원장판본 = "empty-active-fixture" });
    }
    private sealed class R76Vehicle : I기사실차적재근거조회Service
    {
        public int Revision { get; set; } = 1;
        public Task<기사실차적재조회결과> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 기사실차적재조회결과
        { 실차확인됨 = true, 판본 = Revision, 근거지문 = "explicit-fixture-" + Revision, 사유Code = "ActualVehicle.Confirmed",
            유효제원 = new 차량제원 { 차량코드 = "fixture", 최대적재중량Kg = 1000, 운영권장중량Kg = 1000,
                적재함길이Mm = 2000, 적재함폭Mm = 2000, 적재함높이Mm = 2000, 권장최대CBM = 4, 팔레트적재개수 = 2 } });
    }
    private sealed class R76States(DateTime now) : I국내화물운송기사상태Store
    {
        public 국내화물운송기사상태Snapshot Current { get; set; } = new("driver", 1, 상태값.기사운행상태.운행중,
            now, now, 0, 36m, 127m, 5, now, now, null, null, 0, null, null, null);
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult<국내화물운송기사상태Snapshot?>(Current);
        public Task UpsertAsync(국내화물운송기사상태Snapshot state, CancellationToken ct = default) { Current = state; return Task.CompletedTask; }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>([Current]);
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal lat, decimal lng, decimal radius, int take, CancellationToken ct = default)
            => 활성기사조회Async(take, ct);
        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class R76Availability(DateTime observedAtUtc) : I운영배차공통UseCase
    {
        public bool On { get; set; } = true;
        public Task<운영배차수신상태Dto> 수신상태조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 운영배차수신상태Dto
        { 주체Id = id, 수신의사Code = On ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
            실효상태Code = 운영배차실효상태Code.배차가능, 수신의사판본Token = On ? "fixture-on" : "fixture-off",
            수신의사변경시각Utc = new DateTimeOffset(observedAtUtc) });
        public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid rid, string state, string reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto e, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
