using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs.DispatchQueue;

public sealed partial class CargoDispatchJobDomainIsolationTests
{
    [Fact]
    public async Task CargoScan_MixedQueueSelectsOnlyCargoBeforeTheBatchLimit()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        var old = DateTime.UtcNow.AddHours(-2);
        db.운송원장.AddRange(
            Queue(1, "food-planned", 상태값.배차업무유형.음식배달, old),
            Queue(2, "food-waiting", 상태값.배차업무유형.음식배달, old,
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기),
            Queue(3, "food-no-candidate", 상태값.배차업무유형.음식배달, old,
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음),
            Queue(4, "unknown-planned", 99, old),
            Queue(5, "unknown-waiting", 99, old,
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기),
            Queue(6, "cargo-planned", 상태값.배차업무유형.용달운송, old.AddMinutes(1)),
            Queue(7, "cargo-waiting", 상태값.배차업무유형.용달운송, old.AddMinutes(1),
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기));
        var excluded = Queue(8, "cargo-mart-prerequisite", 상태값.배차업무유형.용달운송, old);
        excluded.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트주문;
        db.운송원장.Add(excluded);
        await db.SaveChangesAsync();
        var transition = new RecordingTransition();
        var job = Scan(db, transition.Service, new 배차큐정책Options { 당일미배정공개전환분 = 0 }, batch: 1);

        await job.Execute(Context());

        Assert.Equal(new[]
        {
            (nameof(I배차대기원장전환Service.계획배차에서추천으로전환Async), "cargo-planned"),
            (nameof(I배차대기원장전환Service.추천대기처리Async), "cargo-waiting")
        }, transition.Calls);
        Assert.All(await db.운송원장.Where(x => x.배차업무유형 != 상태값.배차업무유형.용달운송).ToListAsync(),
            queue => Assert.Null(queue.현재추천대상기사Id));
    }

    [Fact]
    public async Task CargoScan_PreservesImmediateAndReservedPublicConversionWithoutTouchingFood()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        var now = DateTime.UtcNow;
        db.운송원장.AddRange(
            Queue(1, "food-old", 상태값.배차업무유형.음식배달, now.AddHours(-8),
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음),
            Queue(2, "cargo-immediate", 상태값.배차업무유형.용달운송, now.AddHours(-6),
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음),
            Queue(3, "cargo-reserved-future", 상태값.배차업무유형.용달운송, now.AddHours(-6),
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음),
            Queue(4, "cargo-reserved-soon", 상태값.배차업무유형.용달운송, now.AddHours(-6),
                상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음));
        db.화주운송의뢰.AddRange(
            new 화주운송의뢰 { 의뢰Id = "cargo-reserved-future", 픽업_시간창_시작일시 = now.AddHours(40) },
            new 화주운송의뢰 { 의뢰Id = "cargo-reserved-soon", 픽업_시간창_시작일시 = now.AddHours(2) });
        await db.SaveChangesAsync();
        var transition = new RecordingTransition();
        var job = Scan(db, transition.Service, new 배차큐정책Options
        {
            당일미배정공개전환분 = 30,
            예약상차전공개전환시간 = 24,
            예약최소추천유지분 = 60
        });

        await job.Execute(Context());

        Assert.Equal(2, transition.Calls.Count);
        Assert.All(transition.Calls, call => Assert.Equal(nameof(I배차대기원장전환Service.공개배차로전환Async), call.Operation));
        Assert.Equal(new[] { "cargo-immediate", "cargo-reserved-soon" }, transition.Calls.Select(x => x.RequestId).OrderBy(x => x));
    }

    [Fact]
    public async Task CargoExpiry_MixedQueueExpiresOnlyCargoBeforeTheBatchLimit()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        var now = DateTime.UtcNow;
        var food = ExpiringQueue(1, "food-expired", 상태값.배차업무유형.음식배달, now.AddMinutes(-5));
        var unknown = ExpiringQueue(2, "unknown-expired", 99, now.AddMinutes(-4));
        var cargo = ExpiringQueue(3, "cargo-expired", 상태값.배차업무유형.용달운송, now.AddMinutes(-3));
        var future = ExpiringQueue(4, "cargo-future", 상태값.배차업무유형.용달운송, now.AddHours(1));
        db.운송원장.AddRange(food, unknown, cargo, future);
        await db.SaveChangesAsync();
        var transition = new RecordingTransition();
        var job = new 추천만료정리Job(db, transition.Service, Enabled(),
            Options.Create(new 배차큐배치작업Options { 처리배치크기 = 1 }), NullLogger<추천만료정리Job>.Instance);

        await job.Execute(Context());

        Assert.Equal(new[] { (nameof(I배차대기원장전환Service.추천만료처리Async), "cargo-expired") }, transition.Calls);
    }

    [Fact]
    public async Task CargoNotificationJob_UsesExplicitCargoPortAndPreservesBatchSize()
    {
        var notifications = new RecordingNotifications();
        var job = new 배차추천알림발송Job(notifications, Enabled(),
            Options.Create(new 배차큐배치작업Options { 처리배치크기 = 7 }), NullLogger<배차추천알림발송Job>.Instance);

        await job.Execute(Context());

        Assert.Equal(0, notifications.LegacyCalls);
        Assert.Equal(new[] { (상태값.배차업무유형.용달운송, 7) }, notifications.Calls);
    }

    [Fact]
    public async Task DisabledCargoWorkflow_DoesNotRunAnyCargoTransitionOrNotification()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        db.운송원장.Add(ExpiringQueue(1, "cargo-expired", 상태값.배차업무유형.용달운송, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var transition = new RecordingTransition();
        var notifications = new RecordingNotifications();
        var activation = new RecordingActivation(false);
        var options = Options.Create(new 배차큐배치작업Options());
        await new 배차큐스캔Job(db, transition.Service, activation, options,
            Options.Create(new 배차큐정책Options()), NullLogger<배차큐스캔Job>.Instance).Execute(Context());
        await new 추천만료정리Job(db, transition.Service, activation, options,
            NullLogger<추천만료정리Job>.Instance).Execute(Context());
        await new 배차추천알림발송Job(notifications, activation, options,
            NullLogger<배차추천알림발송Job>.Instance).Execute(Context());

        Assert.Empty(transition.Calls);
        Assert.Empty(notifications.Calls);
        Assert.Equal(0, notifications.LegacyCalls);
        Assert.Equal(3, activation.Keys.Count);
        Assert.All(activation.Keys, key => Assert.Equal(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch, key));
    }

    [Theory]
    [InlineData(상태값.배차업무유형.용달운송, "cargo", "food")]
    [InlineData(상태값.배차업무유형.음식배달, "food", "cargo")]
    public async Task TypedOutboxSend_BindsQueueIdRequestAndDomainBeforeTakingTheBatch(int domain, string selected, string other)
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        await SeedMixedOutboxesAsync(db);
        var push = new RecordingPush();
        var service = Notifications(db, push);

        var processed = await service.업무유형별대기알림발송Async(domain, take: 1);

        Assert.Equal(1, processed);
        Assert.Equal(selected, Assert.Single(push.RequestIds));
        var items = await db.배차추천알림Outbox.OrderBy(x => x.Id).ToListAsync();
        var sent = Assert.Single(items, x => x.의뢰Id == selected && x.배차대기Id == (selected == "cargo" ? 10 : 20));
        Assert.Equal("Succeeded", sent.발송상태);
        Assert.Equal(1, sent.시도횟수);
        Assert.All(items.Where(x => x.Id != sent.Id), item =>
        {
            Assert.Equal("Pending", item.발송상태);
            Assert.Equal(0, item.시도횟수);
            Assert.Null(item.마지막시도시각);
        });
        Assert.Equal(domain == 상태값.배차업무유형.음식배달 ? "FoodDeliveryRecommendation" : "DriverDispatchRecommendation", push.Types.Single());

        var otherDomain = domain == 상태값.배차업무유형.용달운송 ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송;
        Assert.Equal(1, await service.업무유형별대기알림발송Async(otherDomain, take: 1));
        Assert.Equal(new[] { selected, other }, push.RequestIds);
    }

    [Fact]
    public async Task LegacyOutboxSend_IsCargoOnlyAndCargoFailureDoesNotBlockTheFoodBatch()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        await SeedMixedOutboxesAsync(db);
        var push = new RecordingPush { FailRequestId = "cargo" };
        var service = Notifications(db, push);

        Assert.Equal(1, await service.대기알림발송Async(take: 1));
        Assert.Equal(new[] { "cargo" }, push.RequestIds);
        var cargo = await db.배차추천알림Outbox.SingleAsync(x => x.배차대기Id == 10 && x.의뢰Id == "cargo");
        var food = await db.배차추천알림Outbox.SingleAsync(x => x.배차대기Id == 20 && x.의뢰Id == "food");
        Assert.Equal("Failed", cargo.발송상태);
        Assert.Equal("Pending", food.발송상태);
        Assert.Equal(0, food.시도횟수);

        Assert.Equal(1, await service.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달));
        Assert.Equal("Succeeded", food.발송상태);
        Assert.Equal(new[] { "cargo", "food" }, push.RequestIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task UnknownOutboxDomain_IsRejectedWithoutSendingOrClaimingPendingItems(int domain)
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedMixedOutboxesAsync(fixture.Context);
        var push = new RecordingPush();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Notifications(fixture.Context, push)
            .업무유형별대기알림발송Async(domain));

        Assert.Empty(push.RequestIds);
        Assert.All(await fixture.Context.배차추천알림Outbox.ToListAsync(), item => Assert.Equal(0, item.시도횟수));
    }

    [Fact]
    public async Task LegacyCargoToken_ForTheSameDriverCannotReceiveFoodPushOrClaimTheCargoOutbox()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var db = fixture.Context;
        await SeedMixedOutboxesAsync(db);
        var cargo = await db.배차추천알림Outbox.SingleAsync(x => x.배차대기Id == 10 && x.의뢰Id == "cargo");
        var food = await db.배차추천알림Outbox.SingleAsync(x => x.배차대기Id == 20 && x.의뢰Id == "food");
        cargo.기사Id = food.기사Id = "same-driver";
        (await db.운송원장.SingleAsync(x => x.Id == 20)).현재추천대상기사Id = "same-driver";
        await db.SaveChangesAsync();
        var tokenStore = new LegacyCargoTokenStore();
        var push = new RecordingPush();
        var service = new 배차추천알림Service(db, tokenStore, push, NullLogger<배차추천알림Service>.Instance);

        Assert.Equal(1, await service.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달));

        Assert.Empty(push.RequestIds);
        Assert.Equal(0, tokenStore.LegacyLookups);
        Assert.Equal("Failed", food.발송상태);
        Assert.Equal("Pending", cargo.발송상태);
        Assert.Equal(0, cargo.시도횟수);
        Assert.Null(cargo.마지막시도시각);

        Assert.Equal(1, await service.대기알림발송Async());
        Assert.Equal(new[] { "cargo" }, push.RequestIds);
        Assert.Equal(1, tokenStore.LegacyLookups);
        Assert.Equal("Succeeded", cargo.발송상태);
    }

    [Theory]
    [InlineData(기사앱식별자.FoodDeliveryDriverApp)]
    [InlineData("UnknownDriverApp")]
    [InlineData("")]
    public async Task DefaultTokenAppPort_RejectsFoodAndUnknownAppsWithoutReadingTheCargoToken(string appKey)
    {
        var legacy = new LegacyCargoTokenStore();
        IDriverPushTokenStore store = legacy;

        Assert.Null(await store.GetForAppAsync("same-driver", appKey));
        Assert.Equal(0, legacy.LegacyLookups);
    }

    private static 배차큐스캔Job Scan(SsalddelContext db, I배차대기원장전환Service transition, 배차큐정책Options policy, int batch = 100)
        => new(db, transition, Enabled(), Options.Create(new 배차큐배치작업Options { 처리배치크기 = batch }),
            Options.Create(policy), NullLogger<배차큐스캔Job>.Instance);

    private static RecordingActivation Enabled() => new(true);

    private static IJobExecutionContext Context()
    {
        var context = DispatchProxy.Create<IJobExecutionContext, JobContextProxy>();
        return context;
    }

    private static 운송원장 Queue(long id, string requestId, int domain, DateTime created,
        int stage = 상태값.배차큐단계.계획배차, int exposure = 상태값.배차노출상태.계획대기)
        => new()
        {
            Id = id, 의뢰Id = requestId, 운송번호 = $"transport-{requestId}", 원본의뢰Id = requestId,
            배차업무유형 = domain, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = stage, 배차노출상태 = exposure, CreatedAt = created, UpdatedAt = created
        };

    private static 운송원장 ExpiringQueue(long id, string requestId, int domain, DateTime expiry)
    {
        var queue = Queue(id, requestId, domain, expiry.AddMinutes(-2),
            상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        queue.추천만료시각 = expiry;
        queue.현재추천대상기사Id = $"driver-{requestId}";
        return queue;
    }

    private static async Task SeedMixedOutboxesAsync(SsalddelContext db)
    {
        var now = DateTime.UtcNow;
        var foodQueue = Queue(20, "food", 상태값.배차업무유형.음식배달, now,
            상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        foodQueue.추천라운드 = 1;
        foodQueue.현재추천대상기사Id = "driver-5";
        foodQueue.추천만료시각 = now.AddHours(1);
        db.운송원장.AddRange(
            Queue(10, "cargo", 상태값.배차업무유형.용달운송, now),
            foodQueue,
            Queue(30, "unknown", 99, now));
        // Older opposite-domain, orphan, and mismatched identifiers must not consume this domain's batch.
        db.배차추천알림Outbox.AddRange(
            Outbox(1, 999, "orphan", now.AddMinutes(-10)),
            Outbox(2, 10, "food", now.AddMinutes(-9)),
            Outbox(3, 20, "cargo", now.AddMinutes(-8)),
            Outbox(4, 30, "unknown", now.AddMinutes(-7)),
            Outbox(5, 20, "food", now.AddMinutes(-6)),
            Outbox(6, 10, "cargo", now.AddMinutes(-5)));
        await db.SaveChangesAsync();
    }

    private static 배차추천알림Outbox Outbox(long id, long queueId, string requestId, DateTime created)
        => new()
        {
            Id = id, 배차대기Id = queueId, 의뢰Id = requestId, 기사Id = $"driver-{id}", 추천라운드 = 1,
            제목 = "새로운 배차 추천", 본문 = "근처 운송의뢰가 도착했습니다.", CreatedAt = created, UpdatedAt = created,
            DataJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["type"] = "DriverDispatchRecommendation", ["requestId"] = requestId,
                ["dispatchWaitingId"] = queueId.ToString(), ["recommendationRound"] = "1"
            })
        };

    private static 배차추천알림Service Notifications(SsalddelContext db, RecordingPush push)
        => new(db, new AvailablePushTokenStore(), push, NullLogger<배차추천알림Service>.Instance);

    private sealed class RecordingActivation(bool enabled) : ISsalddelBackgroundJobActivationPolicy
    {
        public List<string> Keys { get; } = [];
        public SsalddelBackgroundWorkloadActivation Evaluate(string workloadKey)
        {
            Keys.Add(workloadKey);
            return new(enabled, enabled ? SsalddelBackgroundWorkloadActivationCodes.Enabled
                : SsalddelBackgroundWorkloadActivationCodes.FeatureDisabled, "DomesticTransportWorkflow");
        }
    }

    public class JobContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_CancellationToken" ? CancellationToken.None
                : throw new InvalidOperationException($"Unexpected Quartz call: {targetMethod?.Name}");
    }

    private sealed class RecordingTransition
    {
        public List<(string Operation, string RequestId)> Calls { get; } = [];
        public I배차대기원장전환Service Service { get; }
        public RecordingTransition()
        {
            Service = DispatchProxy.Create<I배차대기원장전환Service, TransitionProxy>();
            ((TransitionProxy)Service).Calls = Calls;
        }
    }

    public class TransitionProxy : DispatchProxy
    {
        public List<(string Operation, string RequestId)> Calls { get; set; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var requestId = (string)args![0]!;
            Calls.Add((targetMethod!.Name, requestId));
            return Task.FromResult(배차대기원장전환결과.전환됨(requestId, "Recorded", "Recorded"));
        }
    }

    private sealed class RecordingNotifications : I배차추천알림Service
    {
        public List<(int Domain, int Batch)> Calls { get; } = [];
        public int LegacyCalls { get; private set; }
        public Task 추천알림요청생성Async(long queueId, string requestId, string driverId, int round, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
        { LegacyCalls++; return Task.FromResult(0); }
        public Task<int> 업무유형별대기알림발송Async(int 배차업무유형, int take = 100, CancellationToken cancellationToken = default)
        { Calls.Add((배차업무유형, take)); return Task.FromResult(0); }
    }

    private sealed class AvailablePushTokenStore : IDriverPushTokenStore
    {
        public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(driverId);
        public Task<string?> GetForAppAsync(string driverId, string appKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(appKey switch
            {
                기사앱식별자.CargoYongdalDriverApp => $"cargo-fixture:{driverId}",
                기사앱식별자.FoodDeliveryDriverApp => $"food-fixture:{driverId}",
                _ => null
            });
        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class LegacyCargoTokenStore : IDriverPushTokenStore
    {
        public int LegacyLookups { get; private set; }
        public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default)
        { LegacyLookups++; return Task.FromResult<string?>("legacy-cargo-token"); }
        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingPush : IFcmPushService
    {
        public List<string> RequestIds { get; } = [];
        public List<string> Types { get; } = [];
        public string? FailRequestId { get; init; }
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default)
        {
            Assert.True(message.DataOnly);
            Assert.True(message.HighPriority);
            RequestIds.Add(message.Data["offerId"]);
            Types.Add(message.Data["type"]);
            return Task.FromResult(message.Data["offerId"] != FailRequestId);
        }
        public Task<bool> SendToTokenAsync(string token, string title, string body,
            IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
        {
            RequestIds.Add(data["requestId"]);
            Types.Add(data["type"]);
            return Task.FromResult(data["requestId"] != FailRequestId);
        }
    }

    private sealed class TestDatabase(SqliteConnection connection, SsalddelContext context) : IAsyncDisposable
    {
        public SsalddelContext Context { get; } = context;
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,
                new PassThroughEncryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync()
        { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
