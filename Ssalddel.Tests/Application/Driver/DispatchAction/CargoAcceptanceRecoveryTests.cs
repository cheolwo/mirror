using System.Text.Json.Nodes;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed class CargoAcceptanceRecoveryTests
{
    // 개인정보 converter가 최초 protector를 캡처하므로 다른 SQLite fixture의 모델 캐시와 분리합니다.
    internal static readonly ServiceProvider OrdinaryProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, StrategyFactory>()
        .BuildServiceProvider(validateScopes: true);

    [Fact]
    public async Task 수락저장뒤중단된후속은새Context에서재개하고같은수락조회로중복생성하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new Clock();
        await using (var first = Db(connection))
        {
            await first.Database.EnsureCreatedAsync(); await Seed(first);
            var handler = Handler(first, new Continuity(first, clock), new SkippedRecovery());
            Assert.True((await handler.Handle(new("driver", "acceptance-r108"), default)).IsSuccess);
            Assert.Equal("Pending", State(await first.운송이벤트.AsNoTracking().SingleAsync()));
            Assert.Empty(first.Command알림Outbox); Assert.Empty(first.화물운송시간약속);
        }
        await using var db = Db(connection); var continuity = new Continuity(db, clock); var recovery = Service(db, continuity, clock);
        var replay = await Handler(db, continuity, recovery).Handle(new("driver", "acceptance-r108"), default);
        Assert.True(replay.IsSuccess); Assert.Equal(1, continuity.PromiseCalls);
        Assert.True((await Handler(db, continuity, recovery).Handle(new("driver", "acceptance-r108"), default)).IsSuccess);
        Assert.False(await recovery.ProcessRequestAsync("acceptance-r108", "driver"));
        Assert.Equal(0, await recovery.ProcessPendingAsync());
        Assert.Single(await db.운송이벤트.AsNoTracking().ToArrayAsync());
        Assert.Single(await db.화물운송시간약속.AsNoTracking().ToArrayAsync());
        var notification = Assert.Single(await db.Command알림Outbox.AsNoTracking().ToArrayAsync());
        Assert.Equal("Pending", notification.Status); Assert.Equal("DispatchAccepted", notification.FeatureName);
        Assert.Equal("Done", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Empty(db.결제); Assert.Empty(db.기사운송대금지급요청);
    }

    [Fact]
    public async Task 수락트랜잭션이일시실패해도실행전략재시도로후속의도는하나만저장된다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection, retry: true); await db.Database.EnsureCreatedAsync(); await Seed(db);
        var continuity = new Continuity(db, new Clock()) { FailCompletionOnce = true };
        var handler = Handler(db, continuity, new SkippedRecovery());
        Assert.True((await handler.Handle(new("driver", "acceptance-r108"), default)).IsSuccess);
        Assert.True((await handler.Handle(new("driver", "acceptance-r108"), default)).IsSuccess);
        Assert.Equal(2, continuity.CompletionCalls);
        Assert.Single(await db.운송이벤트.AsNoTracking().ToArrayAsync());
        Assert.Equal("Pending", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Empty(db.Command알림Outbox);
    }

    [Fact]
    public async Task 시간약속저장뒤알림실패는후속전체를롤백하고재시작뒤같은의도로다시마친다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var failure = new NotificationFailure(); var clock = new Clock();
        await using (var db = Db(connection, failure))
        {
            await db.Database.EnsureCreatedAsync(); await SeedAccepted(db, clock);
            failure.Armed = true;
            Assert.Equal(1, await Service(db, new Continuity(db, clock), clock).ProcessPendingAsync());
            Assert.Equal("Retry", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
            Assert.Empty(db.화물운송시간약속); Assert.Empty(db.Command알림Outbox);
            var marker = await db.운송이벤트.AsNoTracking().SingleAsync();
            Assert.False(Job(marker)["promisePrepared"]!.GetValue<bool>());
            Assert.DoesNotContain("private-secret", marker.메타데이터);
        }
        clock.Advance();
        await using var fresh = Db(connection);
        Assert.Equal(1, await Service(fresh, new Continuity(fresh, clock), clock).ProcessPendingAsync());
        Assert.Equal("Done", State(await fresh.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Single(fresh.Command알림Outbox); Assert.Single(fresh.화물운송시간약속);
    }

    [Fact]
    public async Task R77_수락후추천재계산실패는기존후속의도로재개하고_완료재실행은중복처리하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new Clock(); var refresh = new RefreshOnce();
        await using (var db = Db(connection))
        {
            await db.Database.EnsureCreatedAsync(); await SeedAccepted(db, clock);
            Assert.Equal(1, await Service(db, new Continuity(db, clock), clock, refresh).ProcessPendingAsync());
            var marker = await db.운송이벤트.AsNoTracking().SingleAsync();
            Assert.Equal("Retry", State(marker));
            Assert.Equal("AcceptanceRecommendationRefreshFailed", Job(marker)["reasonCode"]!.GetValue<string>());
            Assert.False(Job(marker)["recommendationRefreshPrepared"]!.GetValue<bool>());
            Assert.Empty(db.Command알림Outbox); Assert.Empty(db.화물운송시간약속);
        }
        clock.Advance();
        await using var resumed = Db(connection);
        var recovery = Service(resumed, new Continuity(resumed, clock), clock, refresh);
        Assert.Equal(1, await recovery.ProcessPendingAsync());
        Assert.Equal(0, await recovery.ProcessPendingAsync());
        var final = await resumed.운송이벤트.AsNoTracking().SingleAsync();
        Assert.Equal("Done", State(final)); Assert.True(Job(final)["recommendationRefreshPrepared"]!.GetValue<bool>());
        Assert.Equal(2, refresh.Calls);
        Assert.Single(resumed.Command알림Outbox); Assert.Single(resumed.화물운송시간약속);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("other-driver")]
    [InlineData("new-round")]
    [InlineData("same-driver-new-acceptance")]
    public async Task 취소나현재수락세대가달라진이전의도는현재시간약속과알림을변경하지않는다(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); var clock = new Clock(); await SeedAccepted(db, clock);
        var oldId = (await db.운송이벤트.SingleAsync()).Id;
        var row = await db.운송원장.SingleAsync();
        if (scenario == "cancelled") (await db.화주운송의뢰.SingleAsync()).배차상태 = 상태값.배차상태.취소;
        if (scenario == "other-driver") { row.기사_운송자 = "other"; row.확정기사Id = "other"; }
        if (scenario == "new-round") row.추천라운드++;
        if (scenario == "same-driver-new-acceptance") db.운송이벤트.Add(CargoAcceptanceRecoveryMarker.Create(row, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
        Assert.Equal(1, await Service(db, new Continuity(db, clock), clock).ProcessPendingAsync(1));
        Assert.Equal("Blocked", State(await db.운송이벤트.AsNoTracking().SingleAsync(x => x.Id == oldId)));
        Assert.Empty(db.화물운송시간약속); Assert.Empty(db.Command알림Outbox);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 유효한lease는건너뛰고프로세스종료후만료된lease만동일의도로복구한다(bool expired)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); var clock = new Clock(); await SeedAccepted(db, clock);
        var row = await db.운송이벤트.SingleAsync(); var root = JsonNode.Parse(row.메타데이터)!;
        root["cargoAcceptanceFollowups"]!["state"] = "Processing";
        root["cargoAcceptanceFollowups"]!["attempts"] = 1;
        root["cargoAcceptanceFollowups"]!["leaseUntilUtc"] = clock.GetUtcNow().UtcDateTime.AddSeconds(expired ? -1 : 60);
        row.메타데이터 = root.ToJsonString(); await db.SaveChangesAsync();
        Assert.Equal(expired ? 1 : 0, await Service(db, new Continuity(db, clock), clock).ProcessPendingAsync());
        Assert.Equal(expired ? 1 : 0, await db.Command알림Outbox.CountAsync());
    }

    [Fact]
    public async Task 재시도상한과미구성시간약속을완료로숨기지않고과거수락은역생성하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); var clock = new Clock(); await SeedAccepted(db, clock);
        var service = Service(db, new Continuity(db, clock) { SkipPromise = true }, clock);
        Assert.Equal(1, await service.ProcessPendingAsync()); clock.Advance();
        Assert.Equal(1, await service.ProcessPendingAsync()); clock.Advance();
        Assert.Equal(0, await service.ProcessPendingAsync());
        Assert.Equal("Failed", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Empty(db.Command알림Outbox);
        db.운송이벤트.RemoveRange(await db.운송이벤트.ToArrayAsync()); await db.SaveChangesAsync();
        Assert.False(await service.ProcessRequestAsync("acceptance-r108", "driver"));
        Assert.Empty(db.운송이벤트);
    }

    [Fact]
    public async Task 열려있는원업무트랜잭션에서는후속을실행하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); var clock = new Clock(); await SeedAccepted(db, clock);
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = Service(db, new Continuity(db, clock), clock);
        Assert.False(await service.ProcessRequestAsync("acceptance-r108", "driver"));
        Assert.Equal(0, await service.ProcessPendingAsync()); Assert.Empty(db.Command알림Outbox);
    }

    [Fact]
    public async Task 이전수락의시간약속을현재수락의완료근거로사용하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); var clock = new Clock(); await SeedAccepted(db, clock);
        db.화물운송시간약속.Add(new() { 의뢰Id = "acceptance-r108", 기사Id = "driver", Revision = 1,
            잠금시각Utc = clock.GetUtcNow().UtcDateTime.AddDays(-1), CreatedAt = clock.GetUtcNow().UtcDateTime.AddDays(-1) });
        await db.SaveChangesAsync();
        Assert.Equal(1, await Service(db, new Continuity(db, clock), clock).ProcessPendingAsync());
        Assert.Equal("Retry", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Empty(db.Command알림Outbox); Assert.Single(db.화물운송시간약속);
    }

    [Theory]
    [InlineData(false, "Enabled")]
    [InlineData(true, "OperationalModeRequired")]
    [InlineData(true, "FeatureDisabled")]
    public async Task 자동worker는기본OFF와Simulation및화물기능OFF에서복구를호출하지않는다(bool enabled, string reason)
    {
        var recovery = new SkippedRecovery(); var activation = new Activation(reason);
        var collection = new ServiceCollection().AddSingleton<ICargoAcceptanceRecoveryService>(recovery)
            .AddSingleton<ISsalddelBackgroundJobActivationPolicy>(activation);
        await using var provider = collection.BuildServiceProvider();
        using var worker = new CargoAcceptanceRecoveryWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CargoAcceptanceRecoveryOptions { Enabled = enabled }), NullLogger<CargoAcceptanceRecoveryWorker>.Instance);
        await worker.StartAsync(default);
        if (enabled) await activation.Called.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await worker.StopAsync(default);
        Assert.Equal(0, recovery.PendingCalls);
        Assert.Equal(enabled ? 1 : 0, activation.Calls);
    }

    private static CargoAcceptanceRecoveryService Service(SsalddelContext db, Continuity continuity, Clock clock,
        I국내화물추천재검증Service? refresh = null)
        => new(db, continuity, Options.Create(new CargoAcceptanceRecoveryOptions { MaximumAttempts = 2, RetrySeconds = 1 }), clock,
            NullLogger<CargoAcceptanceRecoveryService>.Instance, Options.Create(new 화물연속배차Options { Enabled = true }), refresh);
    private static 배차수락CommandHandler Handler(SsalddelContext db, Continuity continuity, ICargoAcceptanceRecoveryService recovery)
        => new(db, new Publisher(), new User("driver", "기사"), new 참여자실행권한검사(), new WorkRelationshipSnapshotCollector(),
            new Eligibility(), continuity, NullLogger<배차수락CommandHandler>.Instance,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용, acceptanceRecovery: recovery,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);
    private static async Task Seed(SsalddelContext db)
    {
        db.운송원장.Add(new() { 의뢰Id = "acceptance-r108", 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver", 추천라운드 = 1, 추천만료시각 = DateTime.UtcNow.AddMinutes(5) });
        var request = new 화주운송의뢰 { 의뢰Id = "acceptance-r108", 화주Id = "shipper", 주문자UserId = "shipper",
            결제상태 = 상태값.결제상태.결제대기, 배차상태 = 상태값.배차상태.매칭중,
            정산시점 = "운송완료후정산", 정산상태 = "후불승인완료" };
        db.Add(request); await db.SaveChangesAsync();
        var fare = new 운임구성 { 의뢰Id = request.의뢰Id, 기사지급예정운임 = 5000m };
        db.Add(fare); await db.SaveChangesAsync(); request.운임구성Id = fare.Id; await db.SaveChangesAsync();
    }
    private static async Task SeedAccepted(SsalddelContext db, Clock clock)
    {
        await Seed(db); var row = await db.운송원장.SingleAsync();
        row.상태 = 상태값.배차대기상태.확정; row.배차큐단계 = 상태값.배차큐단계.확정;
        row.기사_운송자 = "driver"; row.확정기사Id = "driver";
        (await db.화주운송의뢰.SingleAsync()).배차상태 = 상태값.배차상태.배차확정;
        db.운송이벤트.Add(CargoAcceptanceRecoveryMarker.Create(row, clock.GetUtcNow().UtcDateTime)); await db.SaveChangesAsync();
    }
    private static JsonNode Job(운송이벤트 row) => JsonNode.Parse(row.메타데이터)!["cargoAcceptanceFollowups"]!;
    private static string State(운송이벤트 row) => Job(row)["state"]!.GetValue<string>();
    private static SsalddelContext Db(SqliteConnection connection, NotificationFailure? failure = null, bool retry = false)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
            .UseInternalServiceProvider(retry ? RetryingProvider : OrdinaryProvider);
        if (failure is not null) options.AddInterceptors(failure);
        return new(options.Options, new Encryption());
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Clock : TimeProvider
    { private DateTimeOffset value = DateTimeOffset.UtcNow.AddMinutes(1); public override DateTimeOffset GetUtcNow() => value; public void Advance() => value = value.AddSeconds(2); }
    private sealed class Publisher : IPublisher
    { public Task Publish(object value, CancellationToken ct = default) => throw new IOException("interrupted-event");
      public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => Publish((object)value, ct); }
    private sealed class Eligibility : I화물배차수락적격성Service
    { public Task<화물배차수락적격성결과> 평가Async(string driver, 화주운송의뢰 source, IReadOnlyCollection<string> warnings, CancellationToken ct = default)
        => Task.FromResult(new 화물배차수락적격성결과(true, null, null, [], [], [], null, null)); }
    private sealed class SkippedRecovery : ICargoAcceptanceRecoveryService
    { public int PendingCalls { get; private set; } public Task<bool> ProcessRequestAsync(string request, string driver, CancellationToken ct = default) => Task.FromResult(false);
      public Task<int> ProcessPendingAsync(int take = 20, CancellationToken ct = default) { PendingCalls++; return Task.FromResult(0); } }
    private sealed class RefreshOnce : I국내화물추천재검증Service
    {
        public int Calls { get; private set; }
        public Task<국내화물추천재검증결과> 갱신Async(string driverId, CancellationToken ct = default)
        {
            Calls++;
            if (Calls == 1) throw new IOException("synthetic-refresh-interruption");
            return Task.FromResult(new 국내화물추천재검증결과([], [], []));
        }
    }
    private sealed class Activation(string reason) : ISsalddelBackgroundJobActivationPolicy
    { public int Calls { get; private set; } public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
      public SsalddelBackgroundWorkloadActivation Evaluate(string key) { Calls++; Called.TrySetResult(); return new(reason == "Enabled", reason, "DomesticTransportWorkflow"); } }
    private sealed class NotificationFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Armed && data.Context!.ChangeTracker.Entries<Command알림Outbox>().Any(x => x.State == EntityState.Added))
            { Armed = false; throw new IOException("private-secret"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class TransientFailure : Exception { }
    private sealed class StrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new Strategy(dependencies); }
    private sealed class Strategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailure; }
    private sealed class Continuity(SsalddelContext db, Clock clock) : I화물연속배차UseCase
    {
        public bool FailCompletionOnce { get; set; }
        public bool SkipPromise { get; set; }
        public int CompletionCalls { get; private set; }
        public int PromiseCalls { get; private set; }
        public Task<화물연속배차상태Dto> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물연속배차상태Dto { 기사Id = id });
        public Task<화물연속배차상태Dto> 의사변경Async(string id, 화물연속배차의사변경요청 value, CancellationToken ct = default) => 조회Async(id, ct);
        public Task<화물다음콜예약Dto?> 추천탐색Async(string id, CancellationToken ct = default) => Task.FromResult<화물다음콜예약Dto?>(null);
        public Task<화물예약검증결과> 수락예약검증Async(string id, string request, string? reservation, long? revision, CancellationToken ct = default) => Task.FromResult(new 화물예약검증결과(true));
        public Task 수락완료Async(string id, string request, CancellationToken ct = default)
        { CompletionCalls++; if (FailCompletionOnce && CompletionCalls == 1) throw new TransientFailure(); return Task.CompletedTask; }
        public async Task 시간약속잠금Async(string id, 화주운송의뢰 request, CancellationToken ct = default)
        {
            PromiseCalls++; if (SkipPromise || await db.화물운송시간약속.AnyAsync(x => x.의뢰Id == request.의뢰Id, ct)) return;
            db.화물운송시간약속.Add(new() { 의뢰Id = request.의뢰Id, 기사Id = id, Revision = 1,
                잠금시각Utc = clock.GetUtcNow().UtcDateTime, CreatedAt = clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(ct);
        }
        public Task 완료기록Async(string id, DateTime at, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물경로위험Dto());
    }
}
