using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Data;
using 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs;

public sealed class FoodDeliveryDispatchJobsTests
{
    [Fact]
    public async Task 화물OFF에서도_혼합DB의_음식계획과추천대기만_진행한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        var planned = Queue("food-planned", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-6));
        var waiting = Queue("food-waiting", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-5));
        var retry = Queue("food-no-candidate", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, now.AddMinutes(-4));
        var cargoPlanned = Cargo("cargo-planned", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-9));
        var cargoWaiting = Cargo("cargo-waiting", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-8));
        var cargoRetry = Cargo("cargo-no-candidate", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, now.AddMinutes(-7));
        var mart = Queue("unpacked-mart", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-6));
        mart.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
        var martWaiting = Queue("unpacked-mart-waiting", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-6));
        martWaiting.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트주문;
        var active = Queue("active-driver", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-5));
        active.현재추천대상기사Id = "assigned-driver";
        var completed = Queue("completed", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-5));
        completed.상태 = 상태값.배차상태.인수완료;
        await database.SeedAsync(planned, waiting, retry, cargoPlanned, cargoWaiting, cargoRetry, mart, martWaiting, active, completed);
        var transition = new RecordingTransition();

        await Scan(database.Context, transition).Execute(Context());

        Assert.Equal(["food-planned"], transition.Planned);
        Assert.Equal(["food-waiting", "food-no-candidate"], transition.Waiting);
        Assert.Empty(transition.Expired);
        database.Context.ChangeTracker.Clear();
        var savedCargo = await database.Context.운송원장.AsNoTracking()
            .Where(queue => queue.배차업무유형 == 상태값.배차업무유형.용달운송).OrderBy(queue => queue.의뢰Id).ToArrayAsync();
        Assert.Equal(3, savedCargo.Length);
        Assert.All(savedCargo, queue => Assert.Null(queue.공개전환시각));
        Assert.Equal(상태값.배차큐단계.계획배차, savedCargo.Single(queue => queue.의뢰Id == "cargo-planned").배차큐단계);
    }

    [Fact]
    public async Task 후보없음_재탐색은_음식전용간격이_지난큐만_선택한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        await database.SeedAsync(
            Queue("due", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, now.AddMinutes(-3)),
            Queue("not-due", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, now.AddSeconds(-40)));
        var transition = new RecordingTransition();

        await Scan(database.Context, transition, options: new() { 후보재탐색간격초 = 120 }).Execute(Context());

        Assert.Equal(["due"], transition.Waiting);
        Assert.Empty(transition.Planned);
    }

    [Fact]
    public async Task 포장완료로인계된마트음식은_계획과대기와후보없음에서_음식후속대상이다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow.AddMinutes(-5);
        var planned = Queue("packed-planned", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now);
        var waiting = Queue("packed-waiting", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now);
        var retry = Queue("packed-retry", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, now.AddMinutes(1));
        foreach (var queue in new[] { planned, waiting, retry })
        {
            queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문;
            Assert.True(new 음식배달배차흐름Resolver().Resolve(queue).배차시작가능);
        }
        await database.SeedAsync(planned, waiting, retry);
        var transition = new RecordingTransition();

        await Scan(database.Context, transition).Execute(Context());

        Assert.Equal(["packed-planned"], transition.Planned);
        Assert.Equal(["packed-waiting", "packed-retry"], transition.Waiting);
    }

    [Fact]
    public async Task 준비중마트의_후보없음재시도는_실제음식흐름Port가_준비전으로_차단한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = Queue("unpacked-retry", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음, DateTime.UtcNow.AddMinutes(-5));
        queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
        Assert.False(new 음식배달배차흐름Resolver().Resolve(queue).배차시작가능);
        await database.SeedAsync(queue);
        var candidates = new CandidateSelection { NextDriver = "food-driver" };
        var notifications = new RecordingNotifications();

        await Scan(database.Context, ActualTransition(database.Context, candidates, notifications)).Execute(Context());

        Assert.Empty(candidates.Calls);
        Assert.Empty(notifications.Created);
        database.Context.ChangeTracker.Clear();
        var saved = await database.Context.운송원장.AsNoTracking().SingleAsync();
        Assert.Equal(상태값.배차노출상태.추천후보없음, saved.배차노출상태);
        Assert.Null(saved.현재추천대상기사Id);
        Assert.Null(saved.공개전환시각);
    }

    [Fact]
    public async Task 음식스캔은_각단계의_오래된큐부터_배치한도만큼_처리한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        await database.SeedAsync(
            Queue("plan-newer", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-2)),
            Queue("plan-older", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-3)),
            Queue("wait-newer", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-2)),
            Queue("wait-older", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천대기, now.AddMinutes(-3)));
        var transition = new RecordingTransition();

        await Scan(database.Context, transition, options: new() { 처리배치크기 = 1 }).Execute(Context());

        Assert.Equal(["plan-older"], transition.Planned);
        Assert.Equal(["wait-older"], transition.Waiting);
    }

    [Fact]
    public async Task 만료정리는_혼합DB에서_만료된음식추천만_처리한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        var expired = Recommended("food-expired", now.AddMinutes(-2));
        var cargo = Recommended("cargo-expired", now.AddMinutes(-4));
        cargo.배차업무유형 = 상태값.배차업무유형.용달운송;
        var live = Recommended("food-live", now.AddMinutes(2));
        var unknownExpiry = Recommended("unknown-expiry", null);
        var confirmed = Recommended("food-confirmed", now.AddMinutes(-2));
        confirmed.배차큐단계 = 상태값.배차큐단계.확정;
        var completed = Recommended("food-completed", now.AddMinutes(-2));
        completed.상태 = 상태값.배차상태.인수완료;
        await database.SeedAsync(expired, cargo, live, unknownExpiry, confirmed, completed);
        var transition = new RecordingTransition();

        await Expiry(database.Context, transition).Execute(Context());

        Assert.Equal(["food-expired"], transition.Expired);
        Assert.Empty(transition.Planned);
        Assert.Empty(transition.Waiting);
        database.Context.ChangeTracker.Clear();
        var saved = await database.Context.운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == "cargo-expired");
        Assert.Equal("old-driver", saved.현재추천대상기사Id);
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
    }

    [Fact]
    public async Task 만료정리는_가장오래된음식추천부터_배치한도를_적용한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync(Recommended("newer", DateTime.UtcNow.AddMinutes(-2)), Recommended("older", DateTime.UtcNow.AddMinutes(-3)));
        var transition = new RecordingTransition();

        await Expiry(database.Context, transition, options: new() { 처리배치크기 = 1 }).Execute(Context());

        Assert.Equal(["older"], transition.Expired);
    }

    [Theory]
    [InlineData("scan", SsalddelExecutionMode.Simulation, true, true)]
    [InlineData("expiry", SsalddelExecutionMode.Simulation, true, true)]
    [InlineData("push", SsalddelExecutionMode.Simulation, true, true)]
    [InlineData("scan", SsalddelExecutionMode.Operational, false, true)]
    [InlineData("expiry", SsalddelExecutionMode.Operational, false, true)]
    [InlineData("push", SsalddelExecutionMode.Operational, false, true)]
    public async Task 음식비활성또는Simulation은_화물ON이어도_모든음식후속을_차단한다(
        string jobKind, SsalddelExecutionMode mode, bool foodEnabled, bool cargoEnabled)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync(
            Queue("planned", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, DateTime.UtcNow.AddMinutes(-5)),
            Recommended("expired", DateTime.UtcNow.AddMinutes(-5)));
        var transition = new RecordingTransition();
        var notifications = new RecordingNotifications();
        var policy = Policy(mode, foodEnabled, cargoEnabled);
        IJob job = jobKind switch
        {
            "scan" => Scan(database.Context, transition, policy),
            "expiry" => Expiry(database.Context, transition, policy),
            _ => Push(notifications, policy)
        };

        await job.Execute(Context());

        Assert.Empty(transition.Planned);
        Assert.Empty(transition.Waiting);
        Assert.Empty(transition.Expired);
        Assert.Empty(notifications.Sends);
    }

    [Fact]
    public async Task 화물OFF의_음식알림은_명시음식유형과배치한도와취소토큰을_전달한다()
    {
        var notifications = new RecordingNotifications();
        using var cancellation = new CancellationTokenSource();

        await Push(notifications, options: new() { 처리배치크기 = 7 }).Execute(Context(cancellation.Token));

        var send = Assert.Single(notifications.Sends);
        Assert.Equal(상태값.배차업무유형.음식배달, send.Type);
        Assert.Equal(7, send.Take);
        Assert.Equal(cancellation.Token, send.Token);
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("expiry")]
    [InlineData("push")]
    public async Task 취소된실행은_전환과알림을_시작하지않는다(string jobKind)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync(
            Queue("planned", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, DateTime.UtcNow.AddMinutes(-5)),
            Recommended("expired", DateTime.UtcNow.AddMinutes(-5)));
        var transition = new RecordingTransition();
        var notifications = new RecordingNotifications();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        IJob job = jobKind switch
        {
            "scan" => Scan(database.Context, transition),
            "expiry" => Expiry(database.Context, transition),
            _ => Push(notifications)
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(Context(cancellation.Token)));

        Assert.Empty(transition.Planned);
        Assert.Empty(transition.Waiting);
        Assert.Empty(transition.Expired);
        Assert.Empty(notifications.Sends);
    }

    [Fact]
    public async Task 실제전환Port는_후보없음을_공개화물로넘기지않고_다음스캔에서_음식추천한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        await database.SeedAsync(
            Queue("food", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-5)),
            Cargo("cargo", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기, now.AddMinutes(-5)));
        var candidates = new CandidateSelection();
        var notifications = new RecordingNotifications();
        var transition = ActualTransition(database.Context, candidates, notifications);

        await Scan(database.Context, transition).Execute(Context());

        database.Context.ChangeTracker.Clear();
        var food = await database.Context.운송원장.SingleAsync(queue => queue.의뢰Id == "food");
        Assert.Equal(상태값.배차노출상태.추천후보없음, food.배차노출상태);
        Assert.Equal(상태값.배차큐단계.배차추천, food.배차큐단계);
        Assert.Null(food.공개전환시각);
        Assert.Equal([("food", (string?)null)], candidates.Calls);

        food.UpdatedAt = DateTime.UtcNow.AddMinutes(-2);
        await database.Context.SaveChangesAsync();
        candidates.NextDriver = "new-food-driver";
        await Scan(database.Context, transition).Execute(Context());

        database.Context.ChangeTracker.Clear();
        food = await database.Context.운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == "food");
        var cargo = await database.Context.운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == "cargo");
        Assert.Equal("new-food-driver", food.현재추천대상기사Id);
        Assert.Equal(상태값.배차노출상태.추천중, food.배차노출상태);
        Assert.Null(food.공개전환시각);
        Assert.Equal(상태값.배차큐단계.계획배차, cargo.배차큐단계);
        Assert.Null(cargo.현재추천대상기사Id);
        Assert.All(candidates.Calls, call => Assert.Equal("food", call.RequestId));
        Assert.Equal(["food"], notifications.Created);
    }

    [Fact]
    public async Task 실제만료전환은_이전기사를제외해_다음음식기사에게_추천하고_화물을_보존한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var food = Recommended("food-expired", DateTime.UtcNow.AddMinutes(-2));
        food.추천라운드 = 3;
        var cargo = Recommended("cargo-expired", DateTime.UtcNow.AddMinutes(-2));
        cargo.배차업무유형 = 상태값.배차업무유형.용달운송;
        await database.SeedAsync(food, cargo);
        var candidates = new CandidateSelection { NextDriver = "next-food-driver" };
        var notifications = new RecordingNotifications();

        await Expiry(database.Context, ActualTransition(database.Context, candidates, notifications)).Execute(Context());

        database.Context.ChangeTracker.Clear();
        var saved = await database.Context.운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == "food-expired");
        var savedCargo = await database.Context.운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == "cargo-expired");
        Assert.Equal([("food-expired", (string?)"old-driver")], candidates.Calls);
        Assert.Equal("next-food-driver", saved.현재추천대상기사Id);
        Assert.Equal(4, saved.추천라운드);
        Assert.True(saved.추천만료시각 > DateTime.UtcNow);
        Assert.Equal("old-driver", savedCargo.현재추천대상기사Id);
        Assert.Equal(0, savedCargo.추천라운드);
        Assert.Equal(["food-expired"], notifications.Created);
    }

    private static 음식배달배차큐스캔Job Scan(SsalddelContext db, I배차대기원장전환Service transition,
        ISsalddelBackgroundJobActivationPolicy? policy = null, 음식배달배차배치작업Options? options = null)
        => new(db, transition, policy ?? Policy(), Options.Create(options ?? new()), NullLogger<음식배달배차큐스캔Job>.Instance);

    private static 음식배달추천만료정리Job Expiry(SsalddelContext db, I배차대기원장전환Service transition,
        ISsalddelBackgroundJobActivationPolicy? policy = null, 음식배달배차배치작업Options? options = null)
        => new(db, transition, policy ?? Policy(), Options.Create(options ?? new()), NullLogger<음식배달추천만료정리Job>.Instance);

    private static 음식배달추천알림발송Job Push(I배차추천알림Service notifications,
        ISsalddelBackgroundJobActivationPolicy? policy = null, 음식배달배차배치작업Options? options = null)
        => new(notifications, policy ?? Policy(), Options.Create(options ?? new()), NullLogger<음식배달추천알림발송Job>.Instance);

    private static SsalddelBackgroundJobActivationPolicy Policy(
        SsalddelExecutionMode mode = SsalddelExecutionMode.Operational, bool foodEnabled = true, bool cargoEnabled = false)
        => new(new ExecutionMode(mode), new FeatureFlags(foodEnabled, cargoEnabled), new StaticOptionsMonitor<SalesChannelOrderSyncOptions>(new()));

    private static 운송원장 Queue(string id, int stage, int exposure, DateTime updatedAt)
        => new()
        {
            운송번호 = id, 의뢰Id = id, 원본의뢰Id = id, 화주Id = "orderer",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            상태 = 상태값.배차대기상태.대기, 배차큐단계 = stage, 배차노출상태 = exposure,
            CreatedAt = updatedAt, UpdatedAt = updatedAt
        };

    private static 운송원장 Cargo(string id, int stage, int exposure, DateTime updatedAt)
    {
        var queue = Queue(id, stage, exposure, updatedAt);
        queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        queue.원본의뢰유형 = "CargoTransport";
        return queue;
    }

    private static 운송원장 Recommended(string id, DateTime? expiresAt)
    {
        var queue = Queue(id, 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중, DateTime.UtcNow.AddMinutes(-6));
        queue.현재추천대상기사Id = "old-driver";
        queue.추천만료시각 = expiresAt;
        return queue;
    }

    private static IJobExecutionContext Context(CancellationToken cancellationToken = default)
    {
        var context = DispatchProxy.Create<IJobExecutionContext, JobContextProxy>();
        ((JobContextProxy)(object)context).Token = cancellationToken;
        return context;
    }

    public class JobContextProxy : DispatchProxy
    {
        public CancellationToken Token { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_CancellationToken" ? Token : throw new NotSupportedException(method?.Name);
    }

    private sealed class ExecutionMode(SsalddelExecutionMode mode) : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => mode;
        public bool IsSimulation => mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => mode == SsalddelExecutionMode.Operational;
    }

    private sealed class FeatureFlags(bool food, bool cargo) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string key)
            => key == VersionFeatureFlagKeys.FoodDeliveryWorkflow ? food
                : key == VersionFeatureFlagKeys.DomesticTransportWorkflow && cargo;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>
        {
            [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = food,
            [VersionFeatureFlagKeys.DomesticTransportWorkflow] = cargo
        };
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class RecordingTransition : I배차대기원장전환Service
    {
        public List<string> Planned { get; } = [];
        public List<string> Waiting { get; } = [];
        public List<string> Expired { get; } = [];
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string requestId, CancellationToken cancellationToken = default)
            => Record(Planned, requestId, cancellationToken);
        public Task<배차대기원장전환결과> 추천대기처리Async(string requestId, CancellationToken cancellationToken = default)
            => Record(Waiting, requestId, cancellationToken);
        public Task<배차대기원장전환결과> 추천만료처리Async(string requestId, CancellationToken cancellationToken = default)
            => Record(Expired, requestId, cancellationToken);
        private static Task<배차대기원장전환결과> Record(List<string> calls, string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            calls.Add(id);
            return Task.FromResult(배차대기원장전환결과.전환안됨(id, "Recorded", "Job Port 경계 확인"));
        }
        public Task<배차대기원장전환결과> 추천시작Async(string id, string driverId, int? timeoutSeconds = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driverId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driverId, string? reasonCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 공개배차로전환Async(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException("음식 Job에서 공개 화물 배차를 호출할 수 없습니다.");
        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string id, Ssalddel.Contracts.Common.Operations.DispatchConfirmationBoundaryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 배차수락취소처리Async(string id, string driverId, string? reason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingNotifications : I배차추천알림Service
    {
        public List<(int Type, int Take, CancellationToken Token)> Sends { get; } = [];
        public List<string> Created { get; } = [];
        public Task 추천알림요청생성Async(long queueId, string requestId, string driverId, int round, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Created.Add(requestId);
            return Task.CompletedTask;
        }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("음식 Job은 업무 유형을 명시해야 합니다.");
        public Task<int> 업무유형별대기알림발송Async(int type, int take = 100, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sends.Add((type, take, cancellationToken));
            return Task.FromResult(1);
        }
    }

    private sealed class CandidateSelection : I배차추천후보선정Service
    {
        public string? NextDriver { get; set; }
        public List<(string RequestId, string? ExcludedDriver)> Calls { get; } = [];
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((requestId, 제외기사Id));
            return Task.FromResult(NextDriver is null
                ? 배차추천후보선정결과.적격후보없음("검증 표본에 현재 음식 후보 없음")
                : 배차추천후보선정결과.선정됨(new(NextDriver, 1m, "검증 표본 음식 후보")));
        }
    }

    private static 배차대기원장전환Service ActualTransition(SsalddelContext db, CandidateSelection candidates, RecordingNotifications notifications)
        => new(db, Options.Create(new 배차큐정책Options()), candidates, notifications,
            DispatchProxy.Create<I국내화물운송기사상태Service, DriverStateProxy>(), new 음식배달배차흐름Resolver(),
            음식배달기사추천기록Service: new FoodDriverRecommendationRecorder());

    private sealed class FoodDriverRecommendationRecorder : I음식배달기사추천기록Service
    {
        public Task 추천기록Async(string driverId, DateTime atUtc, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    public class DriverStateProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => throw new NotSupportedException("음식 전환에서 화물 기사 상태를 호출할 수 없습니다: " + method?.Name);
    }

    private sealed class TestDatabase(SqliteConnection connection, SsalddelContext context) : IAsyncDisposable
    {
        public SsalddelContext Context => context;
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async Task SeedAsync(params 운송원장[] queues)
        {
            context.운송원장.AddRange(queues);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
        public async ValueTask DisposeAsync()
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
