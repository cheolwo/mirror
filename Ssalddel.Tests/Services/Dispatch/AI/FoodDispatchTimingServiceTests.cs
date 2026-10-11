using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class FoodDispatchTimingServiceTests
{
    [Theory]
    [InlineData("Disabled")]
    [InlineData("Cargo")]
    [InlineData("Market")]
    [InlineData("ActiveOffer")]
    [InlineData("Retry")]
    [InlineData("Expired")]
    public async Task 적용대상이아니거나재배차기존제안상한이면DB와모델을읽지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.Queue("current");
        var options = new FoodDispatchTimingOptions { Enabled = scenario != "Disabled" };
        if (scenario == "Cargo") queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        if (scenario == "Market") queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
        if (scenario == "ActiveOffer") queue.현재추천대상기사Id = "private-driver";
        if (scenario == "Retry") queue.추천라운드 = 1;
        if (scenario == "Expired") queue.CreatedAt = f.Clock.Now.AddSeconds(-61).UtcDateTime;
        await f.Connection.CloseAsync();

        var result = await f.Service(options).EvaluateAsync(queue);

        Assert.NotEqual("Collect", result.Decision.ActionCode);
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 최근유입이있고현재가용기사가있으면최초접수부터30초까지만수집한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var before = System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync());

        var result = await f.Service().EvaluateAsync(queue);

        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(30), result.Decision.RecheckAtUtc);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(60), result.Decision.HardDeadlineUtc);
        Assert.Equal(1, result.Input.WaitingOrders);
        Assert.Equal(1, result.Input.CompatibleOrders);
        Assert.Equal(0.1m, result.Input.ExpectedNewOrders);
        Assert.Equal(1, result.Input.AvailableDrivers);
        Assert.Equal("Applied", result.AIStatusCode);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()));
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 현재주문만있으면미래유입을만들지않고즉시기존후보를처리한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(arrival: false);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(0m, result.Input.ExpectedNewOrders);
        Assert.Equal("NoExpectedNewOrders", result.Decision.ReasonCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 호환주문이쌓이면모델로더기다리지않고즉시진행한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.Db.운송원장.Add(f.Queue("compatible"));
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(2, result.Input.CompatibleOrders);
        Assert.True(result.Input.PromisingPlanAvailable);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Stale")]
    [InlineData("Future")]
    [InlineData("NoDriver")]
    [InlineData("DuplicateDriver")]
    [InlineData("DuplicatePriority")]
    public async Task 수요나기사정보가미확인이면기다리는판단을모델에맡기지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        if (scenario == "Missing") f.Demand.Value = null;
        if (scenario == "Stale") f.Demand.Value!.판정시각Utc = f.Clock.Now.AddMinutes(-6);
        if (scenario == "Future") f.Demand.Value!.판정시각Utc = f.Clock.Now.AddSeconds(1);
        if (scenario == "NoDriver") f.Demand.Value!.기사판정목록 = [];
        if (scenario == "DuplicateDriver") f.Demand.Value!.기사판정목록 = [Driver("same", 1), Driver("same", 2)];
        if (scenario == "DuplicatePriority") f.Demand.Value!.기사판정목록 = [Driver("first", 1), Driver("second", 1)];
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Database", "SnapshotReadFailed")]
    [InlineData("Truncated", "SnapshotTruncated")]
    [InlineData("NotObserved", "CurrentQueueNotObserved")]
    [InlineData("Region", "RegionUnknown")]
    public async Task 조회실패잘림현재주문누락권역미확인은즉시기존흐름으로복귀한다(string scenario, string status)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var options = new FoodDispatchTimingOptions { Enabled = true };
        if (scenario == "Database") await f.Connection.CloseAsync();
        if (scenario == "Truncated") options.MaximumQueueRows = 1;
        if (scenario == "NotObserved") queue = f.Queue("not-persisted");
        if (scenario == "Region") queue.픽업_위도 = null;
        var result = await f.Service(options).EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(status, result.AIStatusCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 화물마트취소기존추천다른권역은수집대상과신규유입에넣지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(arrival: false);
        foreach (var kind in new[] { "cargo", "market", "cancelled", "offered", "outside" })
        {
            var other = f.Queue(kind);
            if (kind == "cargo") other.배차업무유형 = 상태값.배차업무유형.용달운송;
            if (kind == "market") other.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
            if (kind == "cancelled") other.상태 = 상태값.배차상태.취소;
            if (kind == "offered") { other.현재추천대상기사Id = "driver"; other.CreatedAt = f.Clock.Now.AddHours(-1).UtcDateTime; }
            if (kind == "outside") other.픽업_위도 = 36m;
            f.Db.운송원장.Add(other);
        }
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(1, result.Input.WaitingOrders);
        Assert.Equal(0m, result.Input.ExpectedNewOrders);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Carrier")]
    public async Task 현재원장에확정기사가있으면대기상태오래된투영이어도수집하지않는다(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.Queue("reserved");
        if (kind == "Confirmed") queue.확정기사Id = "driver";
        else queue.기사_운송자 = "driver";
        await f.Connection.CloseAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("ExistingFlow", result.Decision.ActionCode);
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 다른오래된주문과UpdatedAt이현재주문의고정수집기준을바꾸지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.UpdatedAt = f.Clock.Now.AddDays(1).UtcDateTime;
        var other = f.Queue("old-distant");
        other.CreatedAt = f.Clock.Now.AddSeconds(-100).UtcDateTime;
        other.픽업_위도 = 37.52m;
        f.Db.운송원장.Add(other);
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(30), result.Decision.RecheckAtUtc);
    }

    [Theory]
    [InlineData("Timeout")]
    [InlineData("NetworkFailure")]
    [InlineData("InvalidResponse")]
    public async Task 모델실패는기다림을추가하지않는다(string failure)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.AI.Selection = new("Collect", failure);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Null(result.Decision.RecheckAtUtc);
        Assert.Equal(failure, result.AIStatusCode);
    }

    [Fact]
    public async Task 모델응답중수집기한이끝나면Collect응답도서버가기각한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(ageSeconds: 20);
        f.AI.AfterCall = () => f.Clock.Now = f.Clock.Now.AddSeconds(11);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("CollectionElapsed", result.Decision.ReasonCode);
        Assert.Equal("ServerRequiredAfterAI", result.AIStatusCode);
    }

    [Fact]
    public async Task 모델꺼짐은서버의짧은수집판정을사용한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.AI.Enabled = false;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal("Disabled", result.AIStatusCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 첫계획에서추천으로전환된시도1회는재배차로오인하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.계획배차시도횟수 = 1;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.False(result.Input.IsRetry);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(1, f.AI.Calls);
    }

    [Fact]
    public async Task 오래된수요판정시각을현재DB조회시각으로승격하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.Demand.Value!.판정시각Utc = f.Clock.Now.AddSeconds(-30);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(f.Demand.Value.판정시각Utc, result.Input.SnapshotAtUtc);
        Assert.Equal("SnapshotStale", result.Decision.ReasonCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 원주문조리예정완료임박은즉시처리하고미확인은최대수집상한만적용한다(bool readyKnown)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.원본의뢰Id = "original-food-order";
        f.Db.운송원장.Attach(queue);
        f.Db.Entry(queue).Property(x => x.원본의뢰Id).IsModified = true;
        await f.PersistAsync();
        var order = new FoodOrderPort(readyKnown ? f.Clock.Now.AddSeconds(10).UtcDateTime : null);
        var result = await f.Service(order: order).EvaluateAsync(queue);
        Assert.Equal("original-food-order", order.RequestedId);
        Assert.Equal(readyKnown ? "DispatchNow" : "Collect", result.Decision.ActionCode);
        Assert.Equal(readyKnown ? f.Clock.Now.AddSeconds(10) : null, result.Input.LatestDispatchAtUtc);
        Assert.Equal(readyKnown ? 0 : 1, f.AI.Calls);
    }

    [Fact]
    public async Task 잘못된정책설정은예외로주문처리를끊지않고기존배차로돌린다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Connection.CloseAsync();
        var result = await f.Service(new() { Enabled = true, Policy = new() { MaxWaitSeconds = 0 } }).EvaluateAsync(f.Queue("current"));
        Assert.Equal("InvalidConfiguration", result.AIStatusCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 호출자취소는복구판정으로숨기지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().EvaluateAsync(f.Queue("current"), cancellation.Token));
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    private static 음식배달수요기사판정 Driver(string id, int priority)
        => new() { 기사Id = id, 우선순위 = priority, 신규제안허용 = true };

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public SsalddelContext Db { get; } = db;
        public MutableClock Clock { get; } = new();
        public DemandPort Demand { get; } = new();
        public AIPort AI { get; } = new();
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new EncryptionPort());
            await db.Database.EnsureCreatedAsync();
            var f = new Fixture(connection, db);
            f.Demand.Value = new() { 정책판본 = 음식배달수요참여Policy.현재정책판본,
                판정시각Utc = f.Clock.Now, 데이터상태Code = 음식배달수요자료상태Code.최신,
                예상업무량건수 = 1m, 기존기사가용처리건수 = 4m, 기사판정목록 = [Driver("private-driver", 1)] };
            return f;
        }
        public 운송원장 Queue(string id, int ageSeconds = 5) => new()
        {
            의뢰Id = id,
            원본의뢰Id = id, 운송번호 = $"transport-{id}", 배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천대기,
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.501m, 하차_경도 = 127.001m,
            CreatedAt = Clock.Now.AddSeconds(-ageSeconds).UtcDateTime, UpdatedAt = Clock.Now.UtcDateTime
        };
        public async Task<운송원장> SeedAsync(bool arrival = true, int ageSeconds = 5)
        {
            var queue = Queue("current", ageSeconds);
            Db.운송원장.Add(queue);
            if (arrival) { var previous = Queue("completed-arrival", 50); previous.상태 = "완료"; Db.운송원장.Add(previous); }
            await PersistAsync();
            return queue;
        }
        public Task PersistAsync() => PersistCoreAsync();
        private async Task PersistCoreAsync() { await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public FoodDispatchTimingService Service(FoodDispatchTimingOptions? options = null, ISsalddelFoodOrderStore? order = null)
            => new(Db, Demand, AI, Options.Create(options ?? new() { Enabled = true }), new(), Clock, NullLogger<FoodDispatchTimingService>.Instance, order);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class DemandPort : I음식배달수요참여Service
    {
        public 음식배달수요참여결과? Value { get; set; }
        public int Calls { get; private set; }
        public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Value); }
    }
    private sealed class AIPort : IFoodDispatchTimingAI
    {
        public bool Enabled { get; set; } = true;
        public int Calls { get; private set; }
        public Action? AfterCall { get; set; }
        public FoodDispatchTimingAISelection Selection { get; set; } = new("Collect", "Applied");
        public Task<FoodDispatchTimingAISelection> SelectAsync(FoodDispatchTimingInput input, FoodDispatchTimingDecision baseline,
            CancellationToken cancellationToken = default)
        { Calls++; AfterCall?.Invoke(); return Task.FromResult(Selection); }
    }
    private sealed class EncryptionPort : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class FoodOrderPort(DateTime? readyAt) : ISsalddelFoodOrderStore
    {
        public string? RequestedId { get; private set; }
        public 음식주문응답? GetOrder(string orderNo) { RequestedId = orderNo; return new() { 주문번호 = orderNo, 조리예상완료시각Utc = readyAt }; }
        public 음식주문목록응답 GetOrders() => throw new NotSupportedException();
        public 음식주문응답 AddOrder(음식주문등록요청 request) => throw new NotSupportedException();
        public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request) => throw new NotSupportedException();
        public 음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc) => throw new NotSupportedException();
    }
}
