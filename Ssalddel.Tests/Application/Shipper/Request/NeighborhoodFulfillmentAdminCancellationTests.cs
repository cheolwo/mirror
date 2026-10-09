using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Domain.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

/// <summary>격리 SQLite에서 실제 관리자 취소 handler의 모의 수행 경계를 검증합니다. 업체 취소 API·운영 DB 검증은 아닙니다.</summary>
public sealed class NeighborhoodFulfillmentAdminCancellationTests
{
    [Fact]
    public async Task 같은문자열음식주문이있어도_생활배송수행만확인하고_음식기록은보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(NeighborhoodFulfillmentStates.Cancelled);
        var foodAttempt = Attempt(NeighborhoodFulfillmentStates.Accepted);
        foodAttempt.SourceKind = FulfillmentSourceKinds.FoodOrder;
        foodAttempt.AttemptId = "food-attempt";
        fixture.Db.NeighborhoodFulfillmentAttempts.Add(foodAttempt);
        fixture.Db.NeighborhoodFulfillments.Add(new()
        {
            SourceKind = FulfillmentSourceKinds.FoodOrder, RequestId = "request", ActorId = "food-owner",
            AttemptId = foodAttempt.AttemptId, Revision = 1, StateCode = AutoFulfillmentStates.Accepted, IsAutomatic = true
        });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        Assert.True((await fixture.Handler().Handle(Command(), default)).IsSuccess);
        Assert.Equal(AutoFulfillmentStates.Accepted, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking()
            .SingleAsync(x => x.SourceKind == FulfillmentSourceKinds.FoodOrder)).StateCode);
        Assert.Equal(NeighborhoodFulfillmentStates.Accepted, (await fixture.Db.NeighborhoodFulfillmentAttempts.AsNoTracking()
            .SingleAsync(x => x.SourceKind == FulfillmentSourceKinds.FoodOrder)).StateCode);
    }

    [Theory]
    [InlineData(AutoFulfillmentStates.AwaitingRouting, false)]
    [InlineData(AutoFulfillmentStates.NeedsReview, false)]
    [InlineData(AutoFulfillmentStates.Cancelled, true)]
    public async Task 자동수행은_과거시도실패만으로취소하지않고_현재취소확정을요구한다(string state, bool allowed)
    {
        await using var fixture = await Fixture.CreateAsync(NeighborhoodFulfillmentStates.Failed);
        var current = await fixture.Db.NeighborhoodFulfillments.SingleAsync();
        current.IsAutomatic = true;
        current.StateCode = state;
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Handler().Handle(Command(), default);
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? "취소" : "생성됨", (await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync()).상태);
    }

    [Theory]
    [InlineData(NeighborhoodFulfillmentStates.Submitting)]
    [InlineData(NeighborhoodFulfillmentStates.ResultUnknown)]
    [InlineData(NeighborhoodFulfillmentStates.Accepted)]
    [InlineData(NeighborhoodFulfillmentStates.InTransit)]
    [InlineData(NeighborhoodFulfillmentStates.CancelPending)]
    [InlineData(NeighborhoodFulfillmentStates.Completed)]
    [InlineData(NeighborhoodFulfillmentStates.AwaitingSelection)]
    [InlineData("UnexpectedProviderState")]
    public async Task 확인되지않은수행은_관리자취소로원장과정산을종료하지않는다(string state)
    {
        await using var fixture = await Fixture.CreateAsync(state);
        var result = await fixture.Handler().Handle(Command(), default);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Metadata.TryGetValue("ErrorCode", out var code)
            && Equals(code, "FulfillmentCancellationRequired"));
        var source = await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal("생성됨", source.상태);
        Assert.Equal("미시작", source.배차상태);
        Assert.Equal("결제대기", source.결제상태);
        Assert.Equal("모의 수행·정산 제외", source.정산상태);
        Assert.Equal(state, (await fixture.Db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync()).StateCode);
        Assert.Empty(await fixture.Db.운송이벤트.ToListAsync());
        Assert.Empty(await fixture.Db.결제.ToListAsync());
        Assert.Empty(await fixture.Db.운송원장.ToListAsync());
    }

    [Theory]
    [InlineData(NeighborhoodFulfillmentStates.Cancelled)]
    [InlineData(NeighborhoodFulfillmentStates.Failed)]
    public async Task 수행취소또는실패가확인된뒤에는_관리자의뢰취소를허용한다(string state)
    {
        await using var fixture = await Fixture.CreateAsync(state);
        var result = await fixture.Handler().Handle(Command(), default);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var source = await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal("취소", source.상태);
        Assert.Equal("취소", source.배차상태);
        Assert.Equal("결제취소", source.결제상태);
        Assert.True(source.모의처리처수행);
        Assert.Equal(state, (await fixture.Db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync()).StateCode);
        Assert.Single(await fixture.Db.운송이벤트.ToListAsync());
        Assert.Empty(await fixture.Db.결제.ToListAsync());
    }

    [Fact]
    public async Task 미접수의뢰는_시도나큐를만들지않고취소하며_재시도이벤트를중복기록하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = fixture.Handler();
        Assert.True((await handler.Handle(Command(), default)).IsSuccess);
        Assert.True((await handler.Handle(Command(), default)).IsSuccess);
        Assert.Equal("취소", (await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync()).상태);
        Assert.Empty(await fixture.Db.NeighborhoodFulfillments.ToListAsync());
        Assert.Empty(await fixture.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Empty(await fixture.Db.운송원장.ToListAsync());
        Assert.Single(await fixture.Db.운송이벤트.ToListAsync());
    }

    [Theory]
    [InlineData("missing-attempt")]
    [InlineData("orphan-attempt")]
    [InlineData("queue-without-attempt")]
    public async Task 수행기록이불완전하면_미접수로간주해취소하지않는다(string shape)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (shape == "missing-attempt")
            fixture.Db.NeighborhoodFulfillments.Add(new() { RequestId = "request", ActorId = "owner", AttemptId = "missing", Revision = 1 });
        else if (shape == "orphan-attempt") fixture.Db.NeighborhoodFulfillmentAttempts.Add(Attempt(NeighborhoodFulfillmentStates.Submitting));
        else fixture.Db.운송원장.Add(new() { 의뢰Id = "request", 원본의뢰Id = "request", 상태 = "대기" });
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Handler().Handle(Command(), default);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Metadata.TryGetValue("ErrorCode", out var code)
            && Equals(code, "FulfillmentCancellationRequired"));
        Assert.Equal("생성됨", (await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync()).상태);
        Assert.Empty(await fixture.Db.운송이벤트.ToListAsync());
    }

    [Fact]
    public async Task 관리자권한확인이_수행종류안내보다먼저이루어진다()
    {
        await using var fixture = await Fixture.CreateAsync(NeighborhoodFulfillmentStates.Accepted);
        var result = await fixture.Handler("화주").Handle(Command(), default);
        Assert.True(result.IsFailed);
        Assert.Contains("서버관리자", result.Errors[0].Message);
        Assert.DoesNotContain(result.Errors, error => error.Metadata.ContainsKey("ErrorCode"));
        Assert.Empty(await fixture.Db.운송이벤트.ToListAsync());
    }

    [Fact]
    public async Task 모의표시가없는기존운송의_관리자취소동작을유지한다()
    {
        await using var fixture = await Fixture.CreateAsync(NeighborhoodFulfillmentStates.Accepted, simulated: false);
        Assert.True((await fixture.Handler().Handle(Command(), default)).IsSuccess);
        Assert.Equal("취소", (await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync()).상태);
        Assert.Single(await fixture.Db.운송이벤트.ToListAsync());
    }

    private static 관리자운송의뢰취소환불Command Command() => new("request", "request", "사용자 취소 검토 승인");
    private static NeighborhoodFulfillmentAttemptRecord Attempt(string state) => new()
    {
        AttemptId = "attempt", RequestId = "request", ClientRequestId = Guid.NewGuid(), ProviderId = "mock-local",
        QuoteId = "quote", StateCode = state, ExternalRequestId = "mock:attempt", IsSimulation = true,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, EstimatedCompletionUtc = DateTime.UtcNow.AddMinutes(30), Revision = 1
    };

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public static async Task<Fixture> CreateAsync(string? attemptState = null, bool simulated = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            db.화주운송의뢰.Add(new()
            {
                의뢰Id = "request", 주문자UserId = "owner", 화주Id = "owner", 화물종류 = "생활용품",
                상태 = "생성됨", 배차상태 = "미시작", 결제상태 = "결제대기", 정산상태 = "모의 수행·정산 제외",
                모의처리처수행 = simulated, 클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + Guid.NewGuid().ToString("N")
            });
            if (attemptState is not null)
            {
                db.NeighborhoodFulfillmentAttempts.Add(Attempt(attemptState));
                db.NeighborhoodFulfillments.Add(new() { RequestId = "request", ActorId = "owner", AttemptId = "attempt", Revision = 1, UpdatedAtUtc = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new(connection, db);
        }
        public 관리자운송의뢰취소환불CommandHandler Handler(string role = "서버관리자") => new(Db, new Actor(role), new ExecutionPolicy());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed record Actor(string? Role) : ICurrentUserAccessor { public string? UserId => "admin"; }
    private sealed class ExecutionPolicy : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
