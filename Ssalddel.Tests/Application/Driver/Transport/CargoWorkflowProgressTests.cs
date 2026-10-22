using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.도메인.사용자;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoWorkflowProgressTests
{
    [Theory]
    [InlineData("배차확정", "상차지도착", "cargo.arrived-pickup")]
    [InlineData("상차지도착", "상차완료", "cargo.picked-up")]
    [InlineData("상차완료", "하차지도착", "cargo.arrived-dropoff")]
    [InlineData("하차지도착", "인수완료", "cargo.delivered")]
    public async Task 상태저장과알림예약은함께남고_이벤트실패와동일요청재시도에도중복되지않는다(string before, string next, string stage)
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, before);
        var publisher = new Publisher { Fail = true };
        var executor = Executor(db, publisher);
        var request = Request(transport.Id, next);
        Assert.True((await executor.실행Async(request, default)).IsSuccess);
        var row = Assert.Single(await db.Command알림Outbox.AsNoTracking().ToArrayAsync());
        AssertPayload(row, stage, source);
        Assert.Equal(next, (await db.화주운송의뢰.AsNoTracking().SingleAsync()).배차상태);
        Assert.True((await executor.실행Async(request, default)).IsSuccess);
        Assert.Single(await db.Command알림Outbox.ToArrayAsync());
        Assert.Single(await db.운송이벤트.ToArrayAsync());
        Assert.Equal(1, publisher.Calls);
    }

    [Theory]
    [InlineData("상차지도착", "상차지도착", true)]
    [InlineData("배차확정", "인수완료", false)]
    public async Task 동일상태나허용되지않는전이는새진행알림을만들지않는다(string before, string next, bool succeeds)
    {
        await using var db = Memory();
        var (_, transport) = await Seed(db, before);
        var publisher = new Publisher();
        var result = await Executor(db, publisher).실행Async(Request(transport.Id, next), default);
        Assert.Equal(succeeds, result.IsSuccess);
        Assert.Empty(await db.Command알림Outbox.ToArrayAsync());
        Assert.Empty(await db.운송이벤트.ToArrayAsync());
        Assert.Equal(0, publisher.Calls);
    }

    [Fact]
    public async Task 알림insert가실패하면같은SQL저장의운송상태와원장투영도롤백된다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using var db = new SsalddelContext(options, new Encryption());
        await db.Database.EnsureCreatedAsync();
        var (_, transport) = await Seed(db, "배차확정");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_progress BEFORE INSERT ON [Command_알림_Outbox] BEGIN SELECT RAISE(ABORT, 'forced-notification-failure'); END;");
        var publisher = new Publisher();
        await Assert.ThrowsAsync<DbUpdateException>(() => Executor(db, publisher).실행Async(Request(transport.Id, "상차지도착"), default));
        await using var fresh = new SsalddelContext(options, new Encryption());
        Assert.Equal("배차확정", (await fresh.운송원장.AsNoTracking().SingleAsync()).상태);
        Assert.Equal("배차확정", (await fresh.화주운송의뢰.AsNoTracking().SingleAsync()).배차상태);
        Assert.Empty(await fresh.운송이벤트.ToArrayAsync());
        Assert.Empty(await fresh.Command알림Outbox.ToArrayAsync());
        Assert.Equal(0, publisher.Calls);
        Assert.DoesNotContain(db.ChangeTracker.Entries<Command알림Outbox>(), entry => entry.State == EntityState.Added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 알림호환핸들러저장실패나취소뒤다른저장에실패한예약이섞이지않는다(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new NotificationSaveFailure { CancelOnFailure = cancel ? cancellation : null };
        await using var db = Memory(failure);
        var (_, transport) = await Seed(db, "상차지도착");
        var unrelated = new Command알림Outbox { FeatureName = "Unrelated", EventName = "other-operation" };
        db.Command알림Outbox.Add(unrelated);
        failure.Armed = true;
        var value = new 운송상차지도착됨Event("driver", transport.Id, "배차확정", "상차지도착", DateTime.UtcNow, "trace");
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler(db).Handle(value, cancellation.Token));
        else await Handler(db).Handle(value, cancellation.Token);
        Assert.Same(unrelated, Assert.Single(db.Command알림Outbox.Local));
        Assert.Equal(EntityState.Added, db.Entry(unrelated).State);
        await db.SaveChangesAsync();
        Assert.Equal("Unrelated", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).FeatureName);
    }

    [Theory]
    [InlineData("executor")]
    [InlineData("issue")]
    [InlineData("approach")]
    public async Task 화물명령의예약저장실패는그명령이추가한알림만정리한다(string operation)
    {
        var failure = new NotificationSaveFailure();
        await using var db = Memory(failure);
        var (_, transport) = await Seed(db, operation == "issue" ? "운송중" : operation == "approach" ? "이동중" : "배차확정");
        var unrelated = new Command알림Outbox { FeatureName = "Unrelated", EventName = "other-operation" };
        db.Command알림Outbox.Add(unrelated);
        failure.Armed = true;
        Func<Task> action = operation switch
        {
            "executor" => () => Executor(db, new Publisher()).실행Async(Request(transport.Id, "상차지도착"), default),
            "issue" => () => new 운송문제신고CommandHandler(db, new Publisher(), new User(),
                new 참여자실행권한검사(), new 운송증빙첨부JsonWriter(), new 비정상운송사건Service(db),
                NullLogger<운송문제신고CommandHandler>.Instance).Handle(new("driver", transport.Id, "상차", "수량불일치",
                    "private-reason", "private-memo", "private-photo.jpg", "https://private.invalid/photo", true), default),
            _ => () => new 상차접근알림Service(db, new Routes(), NullLogger<상차접근알림Service>.Instance)
                .상차지접근알림검사Async(new("driver", 37.5m, 127.1m, 3m, "운행중", DateTime.UtcNow, DateTime.UtcNow))
        };
        await Assert.ThrowsAsync<IOException>(action);
        Assert.Same(unrelated, Assert.Single(db.Command알림Outbox.Local));
        Assert.Equal(EntityState.Added, db.Entry(unrelated).State);
        await db.SaveChangesAsync();
        Assert.Equal("Unrelated", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).FeatureName);
    }

    [Fact]
    public async Task 접근검사중간실패도앞서예약한자기알림만정리한다()
    {
        await using var db = Memory();
        await Seed(db, "이동중");
        db.Add(new 화주운송의뢰 { 의뢰Id = "request-2", 주문자UserId = "login-user", 화주Id = "business-shipper",
            픽업_위도 = 37.6m, 픽업_경도 = 127.2m });
        db.Add(new 운송원장 { 의뢰Id = "request-2", 운송번호 = "request-2", 기사_운송자 = "driver", 확정기사Id = "driver", 상태 = "이동중" });
        await db.SaveChangesAsync();
        var unrelated = new Command알림Outbox { FeatureName = "Unrelated", EventName = "other-operation" };
        db.Command알림Outbox.Add(unrelated);
        var routes = new Routes { FailOnCall = 2 };
        await Assert.ThrowsAsync<IOException>(() => new 상차접근알림Service(db, routes,
            NullLogger<상차접근알림Service>.Instance).상차지접근알림검사Async(
                new("driver", 37.5m, 127.1m, 3m, "운행중", DateTime.UtcNow, DateTime.UtcNow)));
        Assert.Same(unrelated, Assert.Single(db.Command알림Outbox.Local));
        await db.SaveChangesAsync();
        Assert.Equal("Unrelated", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).FeatureName);
    }

    [Theory]
    [InlineData("stale-state")]
    [InlineData("other-driver")]
    [InlineData("cancelled")]
    public async Task 과거단계나다른담당자의이벤트는원장상태를바꾸거나알림을만들지않는다(string scenario)
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, scenario == "stale-state" ? "인수완료" : "상차지도착");
        if (scenario == "other-driver") transport.확정기사Id = "other-driver";
        if (scenario == "cancelled") source.상태 = "취소";
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.화주운송의뢰.AsNoTracking().SingleAsync());
        await Handler(db).Handle(new 운송상차지도착됨Event("driver", transport.Id, "배차확정", "상차지도착", DateTime.UtcNow, "trace"), default);
        Assert.Equal(before, JsonSerializer.Serialize(await db.화주운송의뢰.AsNoTracking().SingleAsync()));
        Assert.Empty(await db.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 현재이벤트호환처리는알림만한번예약하고정본원장을수정하지않는다()
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, "상차지도착");
        var before = JsonSerializer.Serialize(source);
        var value = new 운송상차지도착됨Event("driver", transport.Id, "배차확정", "상차지도착", DateTime.UtcNow, "trace");
        await Handler(db).Handle(value, default);
        db.ChangeTracker.Clear();
        await Handler(db).Handle(value, default);
        AssertPayload(Assert.Single(await db.Command알림Outbox.ToArrayAsync()), "cargo.arrived-pickup", source);
        Assert.Equal(before, JsonSerializer.Serialize(await db.화주운송의뢰.AsNoTracking().SingleAsync()));
    }

    [Fact]
    public async Task 이전판본알림을보존하고운송ID접두사가같은다른운송은중복으로보지않는다()
    {
        await using var db = Memory();
        var (source, _) = await Seed(db, "상차지도착");
        db.Command알림Outbox.Add(new Command알림Outbox
        {
            EventName = "운송상차지도착됨Event", FeatureName = Command알림FeatureNames.운송상차지도착, Target = "Shipper",
            PayloadJson = "{\"requestId\":\"request-1\",\"transportId\":100}"
        });
        await db.SaveChangesAsync();
        Assert.NotNull(await CargoWorkflowProgressNotifications.ReserveAsync(db, "cargo.arrived-pickup", source, 10, "driver", DateTime.UtcNow, "trace"));
        await db.SaveChangesAsync();
        Assert.Null(await CargoWorkflowProgressNotifications.ReserveAsync(db, "cargo.arrived-pickup", source, 100, "driver", DateTime.UtcNow, "trace"));
        Assert.Equal(2, await db.Command알림Outbox.CountAsync());
    }

    [Theory]
    [InlineData("cargo.accepted")]
    [InlineData("cargo.approaching")]
    [InlineData("cargo.arrived-pickup")]
    [InlineData("cargo.picked-up")]
    [InlineData("cargo.arrived-dropoff")]
    [InlineData("cargo.delivered")]
    [InlineData("cargo.issue")]
    public async Task 단계별payload는기존화물식별자를유지하고개인정보없이인증상세화면을가리킨다(string stage)
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, "배차확정");
        var occurred = new DateTime(2026, 10, 22, 1, 2, 3, DateTimeKind.Utc);
        var row = CargoWorkflowProgressNotifications.Create(stage, source, transport.Id, "driver", occurred);
        AssertPayload(row, stage, source);
        Assert.Equal(row.PayloadJson, CargoWorkflowProgressNotifications.Create(stage, source, transport.Id,
            "driver", DateTime.SpecifyKind(occurred, DateTimeKind.Unspecified)).PayloadJson);
    }

    [Fact]
    public async Task 이전수락알림의요청자와담당기사와수락발생을대조해동일수락을다시예약하지않는다()
    {
        await using var db = Memory();
        var (source, _) = await Seed(db, "배차확정");
        var occurred = new DateTime(2026, 10, 22, 1, 2, 3, DateTimeKind.Utc);
        db.Command알림Outbox.Add(new Command알림Outbox
        {
            FeatureName = "DispatchAccepted", EventName = "배차수락됨Event", Target = "Shipper",
            PayloadJson = JsonSerializer.Serialize(new { requestId = "request-1", driverId = "driver", acceptedAtUtc = occurred })
        });
        await db.SaveChangesAsync();
        Assert.Null(await CargoWorkflowProgressNotifications.ReserveAcceptedAsync(db, source, "driver", occurred, "trace"));
        Assert.NotNull(await CargoWorkflowProgressNotifications.ReserveAcceptedAsync(db, source, "other-driver", occurred, "trace"));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Null(await CargoWorkflowProgressNotifications.ReserveAcceptedAsync(db, source, "other-driver", occurred, "trace"));
        Assert.Equal(2, await db.Command알림Outbox.CountAsync());
    }

    [Fact]
    public async Task 접근알림도인증주문자를수신자로선택하고반복위치갱신은중복예약하지않는다()
    {
        await using var db = Memory();
        var (source, _) = await Seed(db, "이동중");
        var service = new 상차접근알림Service(db, new Routes(), NullLogger<상차접근알림Service>.Instance);
        var location = new DriverLocationSnapshot("driver", 37.5m, 127.1m, 3m, "운행중", DateTime.UtcNow, DateTime.UtcNow);
        Assert.Equal(1, await service.상차지접근알림검사Async(location));
        Assert.Equal(0, await service.상차지접근알림검사Async(location));
        AssertPayload(Assert.Single(await db.Command알림Outbox.ToArrayAsync()), "cargo.approaching", source);
    }

    [Fact]
    public async Task SQLite접근조회도배정운송의의뢰집합을조회하고진행알림을한번만예약한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using var db = new SsalddelContext(options, new Encryption());
        await db.Database.EnsureCreatedAsync();
        var (source, _) = await Seed(db, "이동중");
        var service = new 상차접근알림Service(db, new Routes(), NullLogger<상차접근알림Service>.Instance);
        var location = new DriverLocationSnapshot("driver", 37.5m, 127.1m, 3m, "운행중", DateTime.UtcNow, DateTime.UtcNow);
        Assert.Equal(1, await service.상차지접근알림검사Async(location));
        db.ChangeTracker.Clear();
        Assert.Equal(0, await service.상차지접근알림검사Async(location));
        AssertPayload(Assert.Single(await db.Command알림Outbox.AsNoTracking().ToArrayAsync()), "cargo.approaching", source);
    }

    [Fact]
    public async Task 운송ID가없는기존접근알림도재예약하지않는다()
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, "이동중");
        db.Command알림Outbox.Add(new Command알림Outbox
        {
            EventName = "DriverApproachingPickup", FeatureName = Command알림FeatureNames.상차접근, Target = "Shipper",
            PayloadJson = "{\"requestId\":\"request-1\"}"
        });
        await db.SaveChangesAsync();
        Assert.Null(await CargoWorkflowProgressNotifications.ReserveAsync(db, "cargo.approaching", source, transport.Id, "driver", DateTime.UtcNow, "trace"));
        Assert.Single(await db.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 기사와인증수신자가같으면화주업무ID가달라도자기알림을예약하지않는다()
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, "상차지도착");
        source.주문자UserId = "driver";
        Assert.Null(await CargoWorkflowProgressNotifications.ReserveAsync(db, "cargo.arrived-pickup", source,
            transport.Id, "driver", DateTime.UtcNow, "trace"));
        Assert.Empty(db.Command알림Outbox.Local);
    }

    [Fact]
    public async Task 문제신고저장후이벤트발행이실패해도문제알림은함께남고원문은복제하지않는다()
    {
        await using var db = Memory();
        var (source, transport) = await Seed(db, "운송중");
        var handler = new 운송문제신고CommandHandler(db, new Publisher { Fail = true }, new User(),
            new 참여자실행권한검사(), new 운송증빙첨부JsonWriter(), new 비정상운송사건Service(db),
            NullLogger<운송문제신고CommandHandler>.Instance);
        var result = await handler.Handle(new("driver", transport.Id, "상차", "수량불일치", "private-reason",
            "private-memo", "private-photo.jpg", "https://private.invalid/photo", true), default);
        Assert.True(result.IsSuccess);
        Assert.Single(await db.비정상운송사건.ToArrayAsync());
        var row = Assert.Single(await db.Command알림Outbox.ToArrayAsync());
        AssertPayload(row, "cargo.issue", source);
        Assert.DoesNotContain("private", row.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPayload(Command알림Outbox row, string stageCode, 화주운송의뢰 source)
    {
        var parsed = Command알림Payload.Parse(row.PayloadJson);
        Assert.Equal(WorkflowProgressNotification.MessageType, parsed.NotificationType);
        Assert.Equal("login-user", parsed.TargetUserId);
        Assert.Equal("driver", parsed.DriverId);
        Assert.Equal(source.의뢰Id, parsed.RequestId);
        Assert.Equal(ShipperRequestDetailPageRoutes.SummaryFor(source.의뢰Id), parsed.DeepLink);
        Assert.Equal("Shipper", row.Target);
        Assert.NotEqual("WorkflowProgress", row.FeatureName);
        Assert.Equal(new[] { "Push", "AlimTalk" }, parsed.Channels);
        using var json = JsonDocument.Parse(row.PayloadJson);
        var data = json.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.ToString());
        data["type"] = parsed.NotificationType;
        Assert.True(WorkflowProgressNotification.TryParse(data, out var hint));
        Assert.Equal(stageCode, hint!.StageCode);
        foreach (var name in new[] { "pickupAddress", "dropoffAddress", "pickupContactName", "pickupContactPhone", "driverLatitude", "driverLongitude", "evidence", "memo" })
            Assert.False(json.RootElement.TryGetProperty(name, out _));
        Assert.DoesNotContain("private", row.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(화주운송의뢰, 운송원장)> Seed(SsalddelContext db, string state)
    {
        var source = new 화주운송의뢰
        {
            의뢰Id = "request-1", 화주Id = "business-shipper", 주문자UserId = "login-user", 배차상태 = state,
            픽업_도로명주소 = "private-address", 픽업_상세주소 = "private-detail", 픽업_연락처_이름 = "private-name",
            픽업_연락처_전화번호 = "010-private-phone", 하차_도로명주소 = "private-destination",
            픽업_위도 = 37.5m, 픽업_경도 = 127.1m
        };
        var transport = new 운송원장 { 의뢰Id = source.의뢰Id, 운송번호 = source.의뢰Id,
            화주Id = source.화주Id, 기사_운송자 = "driver", 확정기사Id = "driver", 상태 = state };
        db.Add(source); db.Add(transport); await db.SaveChangesAsync();
        return (source, transport);
    }

    private static 기사운송상태변경요청 Request(long id, string state)
        => new("driver", id, "driver", 살뜰역할유형.기사, state, "ProgressTest", context => new ProgressEvent(context.운송.Id));
    private static 기사운송상태변경CommandExecutor Executor(SsalddelContext db, Publisher publisher)
        => new(db, new 기사운송상태전이Service(), publisher, new User(), new 참여자실행권한검사(),
            NullLogger<기사운송상태변경CommandExecutor>.Instance);
    private static 운송상태화주알림EventHandler Handler(SsalddelContext db) => new(db, NullLogger<운송상태화주알림EventHandler>.Instance);
    private static SsalddelContext Memory(SaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"cargo-progress-{Guid.NewGuid():N}");
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new(builder.Options, new Encryption());
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record User : ICurrentUserAccessor
    { public string? UserId => "driver"; public string? Role => "기사"; }
    private sealed record ProgressEvent(long Id) : INotification;
    private sealed class Publisher : IPublisher
    {
        public bool Fail { get; init; }
        public int Calls { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        { Calls++; return Fail ? Task.FromException(new IOException("event-unavailable")) : Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }
    private sealed class Routes : I배차추천경로Service
    {
        public int FailOnCall { get; init; }
        private int _calls;
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? from, 배차경로좌표? to) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? from, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? from, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 from, 배차경로좌표 to)
        { if (++_calls == FailOnCall) throw new IOException("route-unavailable"); return 1m; }
    }
    private sealed class NotificationSaveFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public CancellationTokenSource? CancelOnFailure { get; init; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Armed) return ValueTask.FromResult(result);
            Armed = false;
            if (CancelOnFailure is not null)
            { CancelOnFailure.Cancel(); throw new OperationCanceledException(CancelOnFailure.Token); }
            throw new IOException("notification-save-unavailable");
        }
    }
}
