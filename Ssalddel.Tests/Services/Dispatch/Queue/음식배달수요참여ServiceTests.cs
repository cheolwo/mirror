using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class 음식배달수요참여ServiceTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 기본OFF나화물요청은DB조회없이기존음식배차흐름을유지한다(bool enabled, bool food)
    {
        await using var f = await Fixture.CreateAsync();
        var options = new 음식배달수요참여Options { Enabled = enabled };
        var queue = f.NewQueue("current");
        if (!food) queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        await f.Connection.CloseAsync();

        Assert.Null(await f.Service(options).평가Async(queue));
        Assert.Equal(0, f.States.Reads);
        Assert.Equal(0, f.Availability.Reads);
    }

    [Fact]
    public async Task 같은권역의현재근무GPS수신의사와실제수락을대조하여기존활동을보호한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current", f.Now.UtcDateTime.AddMinutes(-16));
        f.Db.운송원장.Add(queue);
        await f.AddDriverAsync("incumbent", f.Now.UtcDateTime.AddHours(-1));
        await f.AddDriverAsync("newcomer", f.Now.UtcDateTime.AddMinutes(-5));
        f.Db.음식배달시도.Add(f.NewAttempt("active", "incumbent", f.Now.UtcDateTime.AddMinutes(-20), pickedUp: true));
        await f.PersistAsync();

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        Assert.Equal(음식배달수요자료상태Code.최신, result.데이터상태Code);
        Assert.Equal(1, result.보호활동기사수);
        var incumbent = Assert.Single(result.기사판정목록, x => x.기사Id == "incumbent");
        Assert.True(incumbent.기존활동여부);
        Assert.True(incumbent.신규제안허용);
        Assert.Equal(음식배달수요참여상태Code.기존활동보호, incumbent.상태Code);
        var newcomer = Assert.Single(result.기사판정목록, x => x.기사Id == "newcomer");
        Assert.False(newcomer.신규제안허용);
        Assert.Equal(음식배달수요참여상태Code.수요대기, newcomer.상태Code);
        Assert.Equal(0, f.States.Writes);
        Assert.Equal(0, f.Availability.Writes);
    }

    [Theory]
    [InlineData("Orders", "FoodDemandOrderSnapshotTruncated")]
    [InlineData("Drivers", "FoodDemandDriverSnapshotTruncated")]
    [InlineData("Attempts", "FoodDemandActivitySnapshotTruncated")]
    public async Task 조회가잘린입력은주문이나기사0으로간주하지않고미확인으로돌려준다(string source, string expectedReason)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        f.Db.운송원장.Add(queue);
        await f.AddDriverAsync("driver", f.Now.UtcDateTime.AddMinutes(-5));
        var options = Fixture.EnabledOptions();
        if (source == "Drivers")
        {
            options.최대권역기사수 = 1;
            await f.AddDriverAsync("second-driver", f.Now.UtcDateTime.AddMinutes(-4));
        }
        else
        {
            options.최대조회건수 = 1;
            if (source == "Orders") f.Db.운송원장.Add(f.NewQueue("second-order"));
            if (source == "Attempts")
            {
                f.Db.음식배달시도.Add(f.NewAttempt("first", "driver", f.Now.UtcDateTime.AddMinutes(-3)));
                f.Db.음식배달시도.Add(f.NewAttempt("second", "driver", f.Now.UtcDateTime.AddMinutes(-2)));
            }
        }
        await f.PersistAsync();

        var result = await f.Service(options).평가Async(queue);

        Assert.NotNull(result);
        Assert.Equal(음식배달수요자료상태Code.미확인, result.데이터상태Code);
        Assert.Equal(expectedReason, result.사유Code);
        Assert.Null(result.목표활동인원);
        Assert.Empty(result.기사판정목록);
    }

    [Theory]
    [InlineData("ShiftIdMismatch")]
    [InlineData("CargoShift")]
    [InlineData("StoppedShift")]
    [InlineData("MissingIntentTimestamp")]
    public async Task 실행인덱스가음식앱이어도현재근무나명시수신의사가확인되지않으면적격으로추정하지않는다(string condition)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        f.Db.운송원장.Add(queue);
        var shift = await f.AddDriverAsync("driver", f.Now.UtcDateTime.AddMinutes(-5));
        if (condition == "ShiftIdMismatch") f.States.Replace("driver", x => x with { ShiftId = shift.Id + 100 });
        if (condition == "CargoShift") shift.운송실행유형 = 운송실행유형코드.화물운송;
        if (condition == "StoppedShift") shift.StoppedAtUtc = f.Now.UtcDateTime.AddMinutes(-1);
        if (condition == "MissingIntentTimestamp") f.Availability.IntentChangedAt = null;
        await f.PersistAsync();

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        var driver = Assert.Single(result.기사판정목록);
        Assert.Equal("driver", driver.기사Id);
        Assert.False(driver.신규제안허용);
        Assert.Equal(음식배달수요참여상태Code.적격조건미충족, driver.상태Code);
    }

    [Fact]
    public async Task 픽업권역이확인되지않으면인원을정하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        queue.픽업_위도 = null;

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        Assert.Equal(음식배달수요자료상태Code.미확인, result.데이터상태Code);
        Assert.Equal("FoodDemandRegionUnknown", result.사유Code);
        Assert.Null(result.목표활동인원);
        Assert.Equal(0, f.States.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 유효추천과근무수신의사는수요평가후에도SQL과인덱스에그대로남는다(bool legacyWithoutExpiry)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        f.Db.운송원장.Add(queue);
        await f.AddDriverAsync("driver", f.Now.UtcDateTime.AddMinutes(-10));
        var held = f.NewQueue("held");
        held.현재추천대상기사Id = "driver";
        held.추천시작시각 = f.Now.UtcDateTime.AddSeconds(-10);
        held.추천만료시각 = legacyWithoutExpiry ? null : f.Now.UtcDateTime.AddMinutes(1);
        held.배차큐단계 = 상태값.배차큐단계.배차추천;
        held.배차노출상태 = 상태값.배차노출상태.추천중;
        held.기사지급예정액 = 4000m;
        f.Db.운송원장.Add(held);
        await f.PersistAsync();
        var beforeRows = System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync());
        var beforeShifts = System.Text.Json.JsonSerializer.Serialize(await f.Db.기사근무.AsNoTracking().ToArrayAsync());
        var beforeState = await f.States.GetAsync("driver");

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        Assert.True(Assert.Single(result.기사판정목록).기존활동여부);
        Assert.Equal(beforeRows, System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()));
        Assert.Equal(beforeShifts, System.Text.Json.JsonSerializer.Serialize(await f.Db.기사근무.AsNoTracking().ToArrayAsync()));
        Assert.Equal(beforeState, await f.States.GetAsync("driver"));
        Assert.Equal(0, f.States.Writes);
        Assert.Equal(0, f.Availability.Writes);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task DB조회실패는수요0건이나수신OFF로바꾸지않고미확인으로복구한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        await f.Connection.CloseAsync();

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        Assert.Equal(음식배달수요자료상태Code.미확인, result.데이터상태Code);
        Assert.Equal("FoodDemandSnapshotReadFailed", result.사유Code);
        Assert.Null(result.목표활동인원);
        Assert.Equal(0, f.Availability.Writes);
    }

    [Fact]
    public async Task 같은주문의과거미완료시도보다최신플랫폼중단시도를읽어유령활동을만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.NewQueue("current");
        f.Db.운송원장.Add(queue);
        await f.AddDriverAsync("driver", f.Now.UtcDateTime.AddMinutes(-10));
        f.Db.음식배달시도.Add(f.NewAttempt("same-order", "driver", f.Now.UtcDateTime.AddMinutes(-5)));
        var latest = f.NewAttempt("same-order", "driver", f.Now.UtcDateTime.AddMinutes(-2));
        latest.시도순번 = 2;
        latest.시도StableId += "-2";
        latest.중단시각Utc = f.Now.UtcDateTime.AddMinutes(-1);
        latest.책임Code = 운영배차책임Code.플랫폼;
        latest.상태Code = 음식배달시도상태Code.중단;
        f.Db.음식배달시도.Add(latest);
        await f.PersistAsync();

        var result = await f.Service().평가Async(queue);

        Assert.NotNull(result);
        Assert.Equal(0, result.보호활동기사수);
        var driver = Assert.Single(result.기사판정목록);
        Assert.False(driver.기존활동여부);
        Assert.True(driver.신규제안허용);
        Assert.Equal(음식배달수요참여상태Code.추가참여권고, driver.상태Code);
    }

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public SsalddelContext Db { get; } = db;
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public InMemory음식배달권실행공간Store Spaces { get; } = new();
        public StatePort States { get; } = new();
        public AvailabilityPort Availability { get; } = new();

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new EncryptionPort());
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db);
            fixture.Availability.IntentChangedAt = fixture.Now.AddMinutes(-1);
            return fixture;
        }

        public static 음식배달수요참여Options EnabledOptions()
            => new()
            {
                Enabled = true,
                정책 = new() { 장기대기기회분 = 120m, 장기대기추가인원 = 0, 주문지연추가인원 = 0 }
            };

        public 음식배달수요참여Service Service(음식배달수요참여Options? options = null)
            => new(Db, Spaces, States, Availability, Options.Create(options ?? EnabledOptions()),
                new 음식배달수요참여Policy(), NullLogger<음식배달수요참여Service>.Instance, new Clock(Now));

        public 운송원장 NewQueue(string id, DateTime? createdAt = null)
            => new()
            {
                의뢰Id = id, 원본의뢰Id = id, 운송번호 = $"transport-{id}",
                배차업무유형 = 상태값.배차업무유형.음식배달,
                상태 = 상태값.배차대기상태.대기,
                픽업_위도 = 37.5m, 픽업_경도 = 127m,
                CreatedAt = createdAt ?? Now.UtcDateTime.AddMinutes(-1), UpdatedAt = Now.UtcDateTime
            };

        public 음식배달시도 NewAttempt(string order, string driver, DateTime acceptedAt, bool pickedUp = false)
            => new()
            {
                주문번호 = order, 제안Id = order, 시도StableId = $"attempt-{driver}-{order}",
                기사Id = driver, 시도순번 = 1, Revision = 1,
                상태Code = pickedUp ? 음식배달시도상태Code.픽업완료 : 음식배달시도상태Code.수락,
                수락시각Utc = acceptedAt, 픽업완료시각Utc = pickedUp ? acceptedAt.AddMinutes(1) : null,
                CreatedAtUtc = acceptedAt, UpdatedAtUtc = acceptedAt
            };

        public async Task<기사근무> AddDriverAsync(string id, DateTime startedAt)
        {
            var shift = new 기사근무
            {
                기사Id = id, ActiveDriverId = id, 시작시각 = startedAt,
                운송실행유형 = 운송실행유형코드.음식배달,
                시작모드 = "immediate", 시작위치 = "test-scope", CreatedAt = startedAt, UpdatedAt = startedAt
            };
            Db.기사근무.Add(shift);
            await Db.SaveChangesAsync();
            States.Seed(new(id, shift.Id, 상태값.기사운행상태.운행중, startedAt, startedAt, 0m,
                37.5001m, 127m, 5m, Now.UtcDateTime, Now.UtcDateTime, null, null, 0,
                "immediate", "test-scope", null, AppKey: 기사앱식별자.FoodDeliveryDriverApp));
            var region = 음식배달권정책.판정(new(37.5001m, 127m), null).배달권키;
            await Spaces.Upsert기사Async(region, id, 음식배달권정책.인접배달권키조회(region));
            return shift;
        }

        public async Task PersistAsync() { await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class EncryptionPort : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class StatePort : I국내화물운송기사상태Store
    {
        private readonly Dictionary<string, 국내화물운송기사상태Snapshot> _states = new(StringComparer.Ordinal);
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public void Seed(국내화물운송기사상태Snapshot state) => _states[state.DriverId] = state;
        public void Replace(string id, Func<국내화물운송기사상태Snapshot, 국내화물운송기사상태Snapshot> update) => _states[id] = update(_states[id]);
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(_states.GetValueOrDefault(id)); }
        public Task UpsertAsync(국내화물운송기사상태Snapshot state, CancellationToken cancellationToken = default)
        { Writes++; _states[state.DriverId] = state; return Task.CompletedTask; }
        public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        { Writes++; _states.Remove(id); return Task.CompletedTask; }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal latitude, decimal longitude, decimal radiusKm, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class AvailabilityPort : I운영배차공통UseCase
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public DateTimeOffset? IntentChangedAt { get; set; }
        public Task<운영배차수신상태Dto> 수신상태조회Async(string id, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(new 운영배차수신상태Dto
            { 주체Id = id, 수신의사Code = 운영배차수신의사Code.On, 수신의사변경시각Utc = IntentChangedAt }); }
        public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("ReadOnlyDemandEvaluation"); }
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid requestId, string state, string reason, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("ReadOnlyDemandEvaluation"); }
        public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto item, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("ReadOnlyDemandEvaluation"); }
    }
}
