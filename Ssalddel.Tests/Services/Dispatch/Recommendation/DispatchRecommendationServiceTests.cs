using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Hubs;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Opinet;
using 살뜰.Services.Notifications;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class DispatchRecommendationServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 발송은_조회한_목록과_이벤트와_그룹을_보존하고_push반환값과무관하게_기록한다(bool driving, bool customGroup)
    {
        var ports = new SendPorts { PushResult = false };
        var service = new Probe(ports) { Driving = driving, CustomGroup = customGroup };

        await service.SendToDriverAsync("driver-a");

        Assert.Equal(new[] { driving ? "driving" : "idle", "hub", "push", "log" }, ports.Events);
        Assert.Equal(customGroup ? "custom-driver-a" : "driver-driver-a", ports.GroupName);
        Assert.Equal(DriverDispatchRealtimeContract.RecommendationsEvent, ports.Method);
        Assert.Same(service.Recommendations, Assert.Single(ports.Arguments!));
        Assert.Same(service.Recommendations, ports.Pushed);
        Assert.Equal("driver-a", ports.PushedDriverId);
        Assert.Equal("driver-a", ports.Log!.DriverId);
        Assert.Equal(new[] { "request-b", "request-a" }, ports.Log.RecommendationIds);
        Assert.Equal(2, ports.Log.RecommendationCount);
    }

    [Fact]
    public async Task Hub와_push를_각각_기다린뒤에만_로그와_현재시각을_만든다()
    {
        var hub = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var push = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pushStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var log = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ports = new SendPorts
        {
            HubTask = hub.Task,
            PushOperation = () => { pushStarted.SetResult(); return push.Task; },
            LogOperation = () => { logStarted.SetResult(); return log.Task; }
        };
        var task = new Probe(ports).SendToDriverAsync("driver-a");
        Assert.Equal(new[] { "idle", "hub" }, ports.Events);
        Assert.False(task.IsCompleted);
        hub.SetResult();
        await pushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(ports.Log);
        var afterPushStarted = DateTime.UtcNow;
        push.SetResult();
        await logStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.InRange(ports.Log!.SentAtUtc, afterPushStarted, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, ports.Log.SentAtUtc.Kind);
        Assert.False(task.IsCompleted);
        log.SetResult();
        await task;
    }

    [Theory]
    [InlineData("query", "idle")]
    [InlineData("hub", "idle,hub")]
    [InlineData("push", "idle,hub,push")]
    [InlineData("log", "idle,hub,push,log")]
    public async Task 부분발송실패는_같은오류를_전파하고_후속단계를_실행하거나_자동재시도하지않는다(string stage, string events)
    {
        var ports = new SendPorts { FailureStage = stage };
        var service = new Probe(ports);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendToDriverAsync("driver-a"));

        Assert.Same(ports.Failure, failure);
        Assert.Equal(events.Split(','), ports.Events);
        if (stage != "log") Assert.Null(ports.Log);
    }

    public static IEnumerable<object[]> FuelPrices()
    {
        yield return [Array.Empty<OpinetAveragePriceItem>(), 1700m];
        yield return [new[] { Price("경유", null), Price(null, null) }, 1700m];
        yield return [new[] { Price("휘발유", 1900m), Price("경유", null), Price("자동차용 경유", 1600m), Price("경유", 1500m) }, 1600m];
        yield return [new[] { Price(null, null), Price(null, 1750m), Price("휘발유", 1800m) }, 1750m];
        yield return [new[] { Price("휘발유", 1900m), Price("경유", 0m) }, 0m];
    }

    [Theory]
    [MemberData(nameof(FuelPrices))]
    public async Task 유가는_첫유효경유_첫유효가격_1700원순서를_보존한다(OpinetAveragePriceItem[] items, decimal expected)
    {
        var fuel = new FuelPort { Items = items };
        Assert.Equal(expected, await new Probe(fuel: fuel).FuelPriceAsync());
        Assert.Equal(1, fuel.Calls);
    }

    [Fact]
    public async Task 유가조회오류를_기본가격으로_숨기지않는다()
    {
        var fuel = new FuelPort { Failure = new InvalidOperationException("price unavailable") };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new Probe(fuel: fuel).FuelPriceAsync());
        Assert.Same(fuel.Failure, failure);
        Assert.Equal(1, fuel.Calls);
    }

    [Theory]
    [InlineData(null, 7)]
    [InlineData(" ", 7)]
    [InlineData("기타", 7)]
    [InlineData("전기 냉동 5톤 카고", 999)]
    [InlineData("냉동 5톤 카고", 5.5)]
    [InlineData("5톤 카고", 4.5)]
    [InlineData("대형 용달", 4.5)]
    [InlineData("1톤", 8)]
    [InlineData("카고", 8)]
    [InlineData("용달", 8)]
    public void 연비_기본값과_차종문구_우선순위를_보존한다(string? vehicle, double expected)
        => Assert.Equal((decimal)expected, Probe.Efficiency(vehicle));

    [Theory]
    [InlineData(null, 8, 1700, null)]
    [InlineData(8, 8, null, null)]
    [InlineData(8, 0, 1700, null)]
    [InlineData(8, -1, 1700, null)]
    [InlineData(0, 8, 1700, 0)]
    [InlineData(8, 8, 0, 0)]
    [InlineData(12, 8, 1700, 2550)]
    public void 연료비는_미확인값과_유효연비를_구분하고_반올림하지않는다(int? distance, int efficiency, int? price, int? expected)
        => Assert.Equal((decimal?)expected, Probe.FuelCost(distance, efficiency, price));

    [Fact]
    public void Nullable_경로합산은_전체미확인과_일부확인_0을_구분한다()
    {
        Assert.Null(Probe.Distance()); Assert.Null(Probe.Distance(null, null));
        Assert.Null(Probe.Money()); Assert.Null(Probe.Money(null, null));
        Assert.Null(Probe.Minutes()); Assert.Null(Probe.Minutes(null, null));
        Assert.Equal(0m, Probe.Distance(null, 0m)); Assert.Equal(0m, Probe.Money(null, 0m));
        Assert.Equal(0m, Probe.Minutes(null, TimeSpan.Zero));
        Assert.Equal(3.75m, Probe.Distance(null, 1.25m, 2.5m));
        Assert.Equal(350m, Probe.Money(500m, null, -150m));
        Assert.Equal(1.5m, Probe.Minutes(TimeSpan.FromSeconds(30), null, TimeSpan.FromMinutes(1)));
        Assert.Null(Probe.Point(null, 127m)); Assert.Null(Probe.Point(37m, null));
        Assert.Equal(new 배차경로좌표(0m, 0m), Probe.Point(0m, 0m));
    }

    [Fact]
    public async Task 복귀지는_기사별_UpdatedAt_CreatedAt_내림차순의_최신근무만_읽고_추적수정값을_사용하지않는다()
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var now = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var older = Shift(1, "driver-a", "older", now, now.AddHours(10));
        var firstTie = Shift(2, "driver-a", "first tie", now.AddHours(1), now);
        var latest = Shift(3, "driver-a", "persisted latest", now.AddHours(1), now.AddMinutes(1));
        latest.StoppedAtUtc = now.AddDays(-1);
        var other = Shift(4, "driver-b", "other account", now.AddDays(1), now.AddDays(1));
        fixture.Db.기사근무.AddRange(older, firstTie, latest, other);
        await fixture.Db.SaveChangesAsync();
        latest.오늘의복귀지주소 = "unsaved tracked edit";

        var result = await new Probe(db: fixture.Db).ReturnAsync("driver-a", Driver());

        Assert.Equal("persisted latest", result.주소);
        Assert.Equal(new 배차경로좌표(37.5m, 127m), result.좌표);
        Assert.Equal("오늘복귀지", result.출처);
        Assert.True(result.복귀지기준사용됨);
        Assert.Equal("unsaved tracked edit", latest.오늘의복귀지주소);
    }

    [Theory]
    [InlineData("address")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    public async Task 최신근무가_불완전하면_과거근무를_쓰지않고_기사기본복귀지_그다음없음으로_돌아간다(string missing)
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var now = DateTime.UtcNow;
        var latest = Shift(2, "driver-a", "latest incomplete", now, now);
        if (missing == "address") latest.오늘의복귀지주소 = " ";
        if (missing == "latitude") latest.오늘의복귀지위도 = null;
        if (missing == "longitude") latest.오늘의복귀지경도 = null;
        fixture.Db.기사근무.AddRange(Shift(1, "driver-a", "older complete", now.AddHours(-1), now), latest);
        await fixture.Db.SaveChangesAsync();
        var driver = Driver();
        var service = new Probe(db: fixture.Db);

        var fallback = await service.ReturnAsync("driver-a", driver);
        Assert.Equal(new 복귀지결정결과("driver default", new(0m, 0m), "기본복귀지", true), fallback);
        driver.기본복귀지경도 = null;
        Assert.Equal(new 복귀지결정결과(null, null, "없음", false), await service.ReturnAsync("driver-a", driver));
    }

    [Theory]
    [InlineData("", "오늘복귀지")]
    [InlineData(" ", "오늘복귀지")]
    [InlineData("사용자선택", "사용자선택")]
    public async Task 유효한_오늘복귀지는_기사기본값없이도_원래출처를_보존한다(string source, string expected)
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var shift = Shift(1, "driver-a", "today", DateTime.UtcNow, DateTime.UtcNow);
        shift.복귀지출처 = source;
        fixture.Db.기사근무.Add(shift);
        await fixture.Db.SaveChangesAsync();

        var result = await new Probe(db: fixture.Db).ReturnAsync("driver-a", null);
        Assert.Equal("today", result.주소);
        Assert.Equal(expected, result.출처);
        Assert.True(result.복귀지기준사용됨);
    }

    [Fact]
    public async Task 근무없음과_기사없음을_없음으로_유지하고_조회오류는_기본복귀지로_숨기지않는다()
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var service = new Probe(db: fixture.Db);
        Assert.Equal(new 복귀지결정결과(null, null, "없음", false), await service.ReturnAsync("driver-a", null));
        Assert.Equal("기본복귀지", (await service.ReturnAsync("driver-a", Driver())).출처);
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TABLE driver_shifts");
        await Assert.ThrowsAsync<SqliteException>(() => service.ReturnAsync("driver-a", Driver()));
    }

    private static OpinetAveragePriceItem Price(string? name, decimal? price) => new() { ProductName = name, Price = price };
    private static 용달기사 Driver() => new() { 기사Id = "driver-a", 기본복귀지주소 = "driver default", 기본복귀지위도 = 0m, 기본복귀지경도 = 0m };
    private static 기사근무 Shift(long id, string driverId, string address, DateTime updated, DateTime created)
        => new() { Id = id, 기사Id = driverId, 오늘의복귀지주소 = address, 오늘의복귀지위도 = 37.5m, 오늘의복귀지경도 = 127m, UpdatedAt = updated, CreatedAt = created };

    private sealed class Probe(SendPorts? ports = null, SsalddelContext? db = null, FuelPort? fuel = null)
        : 배차추천Service(db!, null!, null!, ports!, ports!, ports!, null!, null!, null!, null!, null!, fuel!)
    {
        public bool Driving { get; init; }
        public bool CustomGroup { get; init; }
        public IReadOnlyList<DispatchRecommendationDto> Recommendations { get; } = [new() { 의뢰Id = "request-b" }, new() { 의뢰Id = "request-a" }];
        protected override Task<bool> IsDrivingAsync(string driverId) => Task.FromResult(Driving);
        public override Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null) => Query("driving");
        public override Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null) => Query("idle");
        private Task<IReadOnlyList<DispatchRecommendationDto>> Query(string phase)
        {
            ports!.Events.Add(phase);
            return ports.FailureStage == "query" ? Task.FromException<IReadOnlyList<DispatchRecommendationDto>>(ports.Failure) : Task.FromResult(Recommendations);
        }
        protected override string BuildDriverGroup(string driverId) => CustomGroup ? $"custom-{driverId}" : base.BuildDriverGroup(driverId);
        public Task<decimal?> FuelPriceAsync() => ResolveFuelPricePerLiterAsync();
        public Task<복귀지결정결과> ReturnAsync(string driverId, 용달기사? driver) => ResolveReturnDestinationAsync(driverId, driver);
        public static decimal Efficiency(string? vehicle) => ResolveFuelEfficiencyKmPerLiter(vehicle);
        public static decimal? FuelCost(decimal? distance, decimal efficiency, decimal? price) => EstimateFuelCost(distance, efficiency, price);
        public static decimal? Distance(params decimal?[] values) => SumDistance(values);
        public static decimal? Money(params decimal?[] values) => SumMoney(values);
        public static decimal? Minutes(params TimeSpan?[] values) => SumMinutes(values);
        public static 배차경로좌표? Point(decimal? latitude, decimal? longitude) => CreatePoint(latitude, longitude);
    }

    private sealed class SendPorts : IHubContext<DispatchRecommendationHub>, IHubClients, IClientProxy, IDriverRecommendationPushService, IDispatchRecommendationLogStore
    {
        public List<string> Events { get; } = [];
        public string? FailureStage { get; init; }
        public InvalidOperationException Failure { get; } = new("partial send failure");
        public Task HubTask { get; init; } = Task.CompletedTask;
        public Func<Task> PushOperation { get; init; } = () => Task.CompletedTask;
        public Func<Task> LogOperation { get; init; } = () => Task.CompletedTask;
        public bool PushResult { get; init; } = true;
        public string? GroupName { get; private set; }
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }
        public IReadOnlyList<DispatchRecommendationDto>? Pushed { get; private set; }
        public string? PushedDriverId { get; private set; }
        public DispatchRecommendationLogEntry? Log { get; private set; }
        IHubClients IHubContext<DispatchRecommendationHub>.Clients => this;
        IGroupManager IHubContext<DispatchRecommendationHub>.Groups => throw new NotSupportedException();
        public IClientProxy Group(string groupName) { GroupName = groupName; return this; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Events.Add("hub"); Method = method; Arguments = args;
            return FailureStage == "hub" ? Task.FromException(Failure) : HubTask;
        }
        public async Task<bool> SendAsync(string driverId, IReadOnlyList<DispatchRecommendationDto> recommendations, CancellationToken cancellationToken = default)
        {
            Events.Add("push"); PushedDriverId = driverId; Pushed = recommendations;
            if (FailureStage == "push") throw Failure;
            await PushOperation();
            return PushResult;
        }
        public Task AppendAsync(DispatchRecommendationLogEntry entry, CancellationToken cancellationToken = default)
        {
            Events.Add("log"); Log = entry;
            return FailureStage == "log" ? Task.FromException(Failure) : LogOperation();
        }
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public ISingleClientProxy Client(string connectionId) => throw new NotSupportedException();
        IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class FuelPort : IOpinetAveragePriceService
    {
        public IReadOnlyList<OpinetAveragePriceItem> Items { get; init; } = [];
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken cancellationToken = default)
        { Calls++; return Failure is null ? Task.FromResult(Items) : Task.FromException<IReadOnlyList<OpinetAveragePriceItem>>(Failure); }
    }

    private sealed class ReturnFixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public static async Task<ReturnFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
