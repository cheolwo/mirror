using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class 음식주문기사정산Tests
{
    [Fact]
    public async Task 완료대금은동결되며_수령확인과같이공제대기로바뀌고_중복완료는정산을추가하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Context;
        var (order, transport, attempt) = await fixture.SeedAsync(received: false);
        await 음식주문기사정산Recorder.완료기록Async(db, order, transport, attempt, default);
        await db.SaveChangesAsync();
        // 현재 요금 또는 제안이 바뀌어도 완료 원장은 최초 동결 자료를 보존한다.
        transport.기사지급예정액 = 9999m;
        await 음식주문기사정산Recorder.완료기록Async(db, order, transport, attempt, default);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = await db.음식주문기사정산.SingleAsync();
        Assert.Equal(4720m, saved.세전대금);
        Assert.Equal(음식주문기사정산상태Code.수령확인대기, saved.정산상태Code);
        Assert.Null(saved.공제액);
        Assert.Null(saved.수령액);
        var store = new EfSsalddelFoodOrderStore(db);
        var payload = new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid() };
        Assert.True(store.주문자수령확인(order.주문번호, payload, "orderer-1")!.새로변경됨);
        Assert.False(store.주문자수령확인(order.주문번호, payload, "orderer-1")!.새로변경됨);
        db.ChangeTracker.Clear();
        saved = await db.음식주문기사정산.SingleAsync();
        Assert.Equal(음식주문기사정산상태Code.공제확인대기, saved.정산상태Code);
        Assert.NotNull(saved.수령확인시각Utc);
        Assert.Equal(2, saved.Revision);
        Assert.Null(saved.수령액);
    }

    [Fact]
    public async Task 근거없는요금은계산값으로채우지않고_공제미확정과운영실행도지급성공으로바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(received: true);
        transport.기사제안요금계산근거Json = "{\"unverified\":true}";
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        var settlement = await fixture.Context.음식주문기사정산.SingleAsync();
        Assert.Null(settlement.세전대금);
        Assert.Equal(음식주문기사정산상태Code.요금근거없음, settlement.정산상태Code);
        var missing = Request(settlement);
        missing.ConfirmedDeductionAmount = null;
        Assert.True((await fixture.UseCase().모의지급검증Async(order.주문번호, missing, default)).IsFailed);
        Assert.True((await fixture.UseCase(SsalddelExecutionMode.Operational)
            .모의지급검증Async(order.주문번호, Request(settlement), default)).IsFailed);
        Assert.Empty(await fixture.Context.음식주문기사지급검증.ToListAsync());
        Assert.Equal(음식주문기사지급상태Code.미요청, settlement.지급상태Code);
        Assert.Null(settlement.수령액);
    }

    [Fact]
    public async Task 모의실패와재시도는각각보존되며_동일요청재처리와다른키중복성공을구분한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(received: true);
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        var settlement = await fixture.Context.음식주문기사정산.SingleAsync();
        var service = fixture.UseCase();
        var failed = Request(settlement, "failed-key", "Failed");
        var failedResult = await service.모의지급검증Async(order.주문번호, failed, default);
        Assert.True(failedResult.IsSuccess);
        Assert.Equal(음식주문기사지급상태Code.모의실패, failedResult.Value.PayoutStatusCode);
        var duplicateFailure = await service.모의지급검증Async(order.주문번호, failed, default);
        Assert.True(duplicateFailure.Value.IsIdempotentReplay);
        var succeeded = Request(settlement, "success-key");
        var successResult = await service.모의지급검증Async(order.주문번호, succeeded, default);
        Assert.True(successResult.IsSuccess);
        Assert.Equal(4620m, successResult.Value.NetAmount);
        Assert.Equal("SimulationFixture", successResult.Value.DeductionEvidenceScopeCode);
        Assert.Equal("Simulation", successResult.Value.ExecutionModeCode);
        Assert.Equal("Simulation", successResult.Value.ServerExecutionModeCode);
        Assert.False(successResult.Value.IsActualTransferCompleted);
        Assert.Equal(2, successResult.Value.SimulationPayments.Count);
        var replay = await service.모의지급검증Async(order.주문번호, succeeded, default);
        Assert.True(replay.Value.IsIdempotentReplay);
        Assert.Equal("Simulation", replay.Value.ServerExecutionModeCode);
        var mismatched = Request(settlement, "success-key");
        mismatched.ConfirmedDeductionAmount = 101;
        Assert.True((await service.모의지급검증Async(order.주문번호, mismatched, default)).IsFailed);
        Assert.True((await service.모의지급검증Async(order.주문번호, Request(settlement, "new-success-key"), default)).IsFailed);
        fixture.Context.ChangeTracker.Clear();
        var trace = await new 음식주문운영추적UseCase(fixture.Context).조회Async(order.주문번호);
        Assert.Equal(4720m, trace!.DriverSettlement!.GrossAmount);
        Assert.Equal(4620m, trace.DriverSettlement.NetAmount);
        Assert.Equal(음식주문기사지급상태Code.모의성공, trace.DriverSettlement.PayoutStatusCode);
        Assert.False(trace.DriverSettlement.IsActualTransferCompleted);
        Assert.Equal(2, await fixture.Context.음식주문기사지급검증.CountAsync());
        Assert.Equal(음식주문상태코드.수령확인, (await fixture.Context.음식주문.SingleAsync()).상태);
    }

    [Fact]
    public async Task 현재서버가Operational로바뀌어도_과거모의증빙을보존하며현재모드를별도로조회한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(received: true);
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        var settlement = await fixture.Context.음식주문기사정산.SingleAsync();
        var simulated = await fixture.UseCase().모의지급검증Async(order.주문번호, Request(settlement), default);
        Assert.True(simulated.IsSuccess);

        fixture.Context.ChangeTracker.Clear();
        var trace = await new 음식주문운영추적UseCase(fixture.Context, new Execution(SsalddelExecutionMode.Operational))
            .조회Async(order.주문번호);
        var dto = trace!.DriverSettlement!;
        Assert.Equal("Operational", dto.ServerExecutionModeCode);
        Assert.Equal("Simulation", dto.ExecutionModeCode);
        Assert.Equal("Simulation", Assert.Single(dto.SimulationPayments).ExecutionModeCode);
        Assert.Equal(4620m, dto.NetAmount);
        Assert.False(dto.IsActualTransferCompleted);
        Assert.False(Assert.Single(dto.SimulationPayments).IsActualTransferCompleted);
        Assert.Equal("Simulation", (await fixture.Context.음식주문기사정산.AsNoTracking().SingleAsync()).실행모드Code);
    }

    [Fact]
    public void 현재모드없는DTO변환은_과거증빙모드를서버모드로추론하지않는다()
    {
        var settlement = new 음식주문기사정산
        {
            실행모드Code = "Simulation",
            지급검증목록 = [new 음식주문기사지급검증 { 지급StableId = "prior-proof", 결과Code = "Succeeded" }]
        };
        var dto = 음식주문기사정산Recorder.ToDto(settlement);
        Assert.Equal("Unknown", new FoodDeliveryOrderSettlementDto().ServerExecutionModeCode);
        Assert.Equal("Unknown", dto.ServerExecutionModeCode);
        Assert.Equal("Simulation", dto.ExecutionModeCode);
        Assert.Equal("Simulation", Assert.Single(dto.SimulationPayments).ExecutionModeCode);
        Assert.False(dto.IsActualTransferCompleted);
    }

    [Theory]
    [InlineData("not-received")]
    [InlineData("cancelled")]
    [InlineData("reassigned")]
    [InlineData("unauthorized")]
    [InlineData("stale-revision")]
    public async Task 수령미확인_취소_재배차_권한_판본충돌은증빙을생성하지않는다(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(received: scenario != "not-received");
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        var settlement = await fixture.Context.음식주문기사정산.SingleAsync();
        if (scenario == "cancelled") order.상태 = 음식주문상태코드.취소;
        if (scenario == "reassigned")
        {
            transport.확정기사Id = "driver-2";
            fixture.Context.음식배달시도.Add(new 음식배달시도
            {
                시도StableId = "attempt-2", 주문번호 = order.주문번호, 제안Id = transport.의뢰Id,
                기사Id = "driver-2", 시도순번 = 2, 수락시각Utc = DateTime.UtcNow
            });
        }
        await fixture.Context.SaveChangesAsync();
        var request = Request(settlement);
        if (scenario == "stale-revision") request.ExpectedSettlementRevision += 1;
        var result = await fixture.UseCase(role: scenario == "unauthorized" ? 역할명.기사 : 역할명.서버관리자)
            .모의지급검증Async(order.주문번호, request, default);
        Assert.True(result.IsFailed);
        Assert.Equal(scenario == "unauthorized" ? 403 : 409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(0, await fixture.Context.음식주문기사지급검증.CountAsync());
        fixture.Context.ChangeTracker.Clear();
        Assert.Null((await fixture.Context.음식주문기사정산.SingleAsync()).수령액);
    }

    [Fact]
    public async Task 서로다른동시요청은동일정산판본을두번성공시키지못한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(received: true);
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        var settlement = await fixture.Context.음식주문기사정산.SingleAsync();
        await using var second = fixture.NewContext();
        var stale = await second.음식주문기사정산.SingleAsync();
        Assert.True((await fixture.UseCase().모의지급검증Async(order.주문번호, Request(settlement), default)).IsSuccess);
        var other = fixture.UseCase(context: second);
        Assert.True((await other.모의지급검증Async(order.주문번호, Request(stale, "other-key"), default)).IsFailed);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Context.음식주문기사지급검증.CountAsync());
        Assert.Equal(음식주문기사지급상태Code.모의성공, (await fixture.Context.음식주문기사정산.SingleAsync()).지급상태Code);
    }

    private static FoodDeliverySimulatedPayoutRequest Request(음식주문기사정산 settlement, string key = "test-key", string outcome = "Succeeded")
        => new()
        {
            IdempotencyKey = key,
            ExpectedSettlementRevision = settlement.Revision,
            ConfirmedGrossAmount = 4720m,
            ConfirmedDeductionAmount = 100m,
            DeductionEvidenceReference = "isolated-simulation-fixture-100-krw-not-statutory",
            OutcomeCode = outcome
        };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SsalddelContext> _options;
        private Fixture(SqliteConnection connection, DbContextOptions<SsalddelContext> options)
        {
            _connection = connection;
            _options = options;
            Context = NewContext();
        }
        public SsalddelContext Context { get; }
        public SsalddelContext NewContext() => new(_options, new Encryption());
        public 음식주문기사정산UseCase UseCase(SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation,
            string role = 역할명.서버관리자, SsalddelContext? context = null)
            => new(context ?? Context, new User("admin-1", role), new Execution(mode), TimeProvider.System);
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var result = new Fixture(connection, new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options);
            await result.Context.Database.EnsureCreatedAsync();
            return result;
        }
        public async Task<(음식주문, 운송원장, 음식배달시도)> SeedAsync(bool received)
        {
            var now = DateTime.UtcNow;
            var order = new 음식주문
            {
                주문번호 = "food-order-1", 주문자UserId = "orderer-1", 음식점명 = "검증 식당",
                상태 = received ? 음식주문상태코드.수령확인 : 음식주문상태코드.전달완료,
                배차상태 = 음식주문배차상태코드.배달완료,
                UpdatedAt = now
            };
            var transport = new 운송원장
            {
                의뢰Id = "offer-1", 원본의뢰Id = order.주문번호, 원본의뢰유형 = "음식점주문",
                운송번호 = "transport-1", 배차업무유형 = 상태값.배차업무유형.음식배달,
                확정기사Id = "driver-1", 기사_운송자 = "driver-1",
                상태 = 상태값.배차상태.인수완료, 배차큐단계 = 상태값.배차큐단계.종료,
                기사지급예정액 = 4720m, 기사제안요금정책판본 = "frozen.r1", 기사제안요금판정시각Utc = now,
                기사제안요금계산근거Json = JsonSerializer.Serialize(new { 요금 = new { 기사지급예정액 = 4720m }, 정책판본 = "frozen.r1", 기사Id = "driver-1" })
            };
            var attempt = new 음식배달시도
            {
                시도StableId = "attempt-1", 주문번호 = order.주문번호, 제안Id = transport.의뢰Id,
                기사Id = "driver-1", 시도순번 = 1, 수락시각Utc = now.AddMinutes(-20),
                픽업완료시각Utc = now.AddMinutes(-10), 전달완료시각Utc = now,
                상태Code = 음식배달시도상태Code.전달완료
            };
            Context.음식주문.Add(order);
            Context.운송원장.Add(transport);
            Context.음식배달시도.Add(attempt);
            await Context.SaveChangesAsync();
            order.배차대기Id = transport.Id;
            await Context.SaveChangesAsync();
            return (order, transport, attempt);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed record Execution(SsalddelExecutionMode Mode) : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
