using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Food;

public sealed class FoodDeliveryDailySettlementTests
{
    private static readonly DateOnly Day = new(2026, 10, 3);
    private static readonly DateTime Start = new(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 당일전체55건과최근40건은다른조회이며_다른기사와다른날을합산하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (var i = 0; i < 55; i++)
            await fixture.RecordAsync(Start.AddMinutes(i), 3000m + i);
        await fixture.RecordAsync(Start.AddHours(2), 9000m, "other-driver");
        await fixture.RecordAsync(Start.AddDays(-1), 8000m);
        var daily = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        var workspace = await fixture.UseCase().GetAsync("driver-1", default);
        Assert.Equal(55, daily.CompletedOrderCount);
        Assert.Equal(55, daily.OrderSettlements.Count);
        Assert.Equal(40, workspace.OrderSettlements.Count);
        Assert.Equal(Enumerable.Range(0, 55).Sum(i => 3000m + i), daily.GrossAmountTotal);
        Assert.All(daily.OrderSettlements, x => Assert.Equal("driver-1", x.DriverId));
        Assert.Null(daily.DeductionAmountTotal);
        Assert.Null(daily.NetAmountTotal);
        Assert.True(daily.IsFullDayQuery);
        Assert.Equal("DeliveryCompletedAt", daily.DateBasisCode);
    }

    [Fact]
    public async Task 한국자정반개구간은시작을포함하고다음자정과전날을제외한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.RecordAsync(Start.AddTicks(-1), 1000m);
        var first = await fixture.RecordAsync(Start, 2000m);
        var last = await fixture.RecordAsync(Start.AddDays(1).AddTicks(-1), 3000m);
        await fixture.RecordAsync(Start.AddDays(1), 4000m);
        var result = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(Start, result.PeriodStartAtUtc);
        Assert.Equal(Start.AddDays(1), result.PeriodEndAtUtc);
        Assert.Equal(new[] { first.주문번호, last.주문번호 }, result.OrderSettlements.Select(x => x.OrderNo));
        Assert.Equal(5000m, result.GrossAmountTotal);
        Assert.Equal("Asia/Seoul", result.TimeZoneCode);
    }

    [Fact]
    public async Task 날짜미입력은UTC오늘이아닌서버한국오늘을사용한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var now = new DateTimeOffset(2026, 9, 30, 16, 0, 0, TimeSpan.Zero);
        var result = await fixture.UseCase(now).GetDailySettlementAsync("driver-1", null, default);
        Assert.Equal(new DateOnly(2026, 10, 1), result.CompletionDateKst);
        Assert.Equal(now.UtcDateTime, result.UpdatedAtUtc);
    }

    [Fact]
    public async Task 다음날수령확인과공제모의조정은완료일귀속과동결세전대금을옮기지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.RecordAsync(Start.AddHours(4), 4720m, received: false);
        var before = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(1, before.AwaitingReceiptOrderCount);
        Assert.Null(before.NetAmountTotal);
        item.수령확인시각Utc = Start.AddDays(1).AddHours(5);
        item.공제액 = 100m;
        item.수령액 = 4620m;
        item.공제근거참조 = "explicit-isolated-fixture";
        item.공제근거범위Code = "SimulationFixture";
        item.정산상태Code = 음식주문기사정산상태Code.모의검증준비;
        item.지급상태Code = 음식주문기사지급상태Code.모의성공;
        item.실행모드Code = "Simulation";
        item.Revision++;
        item.UpdatedAtUtc = Start.AddDays(1).AddHours(6);
        item.지급검증목록.Add(new 음식주문기사지급검증
        {
            지급StableId = "simulation-later", 멱등키 = "later-key", 결과Code = "Succeeded",
            모의수령액 = 4620m, 확인세전대금 = 4720m, 확인공제액 = 100m,
            검증시각Utc = item.UpdatedAtUtc
        });
        await fixture.Context.SaveChangesAsync();
        var after = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        var nextDay = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day.AddDays(1), default);
        Assert.Equal(4720m, after.GrossAmountTotal);
        Assert.Equal(100m, after.DeductionAmountTotal);
        Assert.Equal(4620m, after.NetAmountTotal);
        Assert.Equal(1, after.ReceiptConfirmedOrderCount);
        Assert.Equal(1, after.SimulationSucceededOrderCount);
        Assert.Equal(4620m, after.SimulationSucceededNetAmountTotal);
        Assert.Equal(0, nextDay.CompletedOrderCount);
        Assert.Null(after.ActualTransferAmountTotal);
        Assert.Equal(0, after.ActualTransferCompletedOrderCount);
        Assert.False(Assert.Single(after.OrderSettlements).IsActualTransferCompleted);
    }

    [Fact]
    public async Task 일부금액과공제가미확정이면전체합계는null이고확인분만별도로합산한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.RecordAsync(Start.AddHours(1), 4000m);
        var missing = await fixture.RecordAsync(Start.AddHours(2), 5000m);
        missing.세전대금 = null;
        missing.요금계산근거Json = "{}";
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(4000m, result.KnownGrossAmountTotal);
        Assert.Null(result.GrossAmountTotal);
        Assert.Null(result.DeductionAmountTotal);
        Assert.Null(result.NetAmountTotal);
        Assert.Equal(1, result.MissingGrossAmountCount);
        Assert.Equal(2, result.UnconfirmedDeductionCount);
        Assert.Equal(2, result.UnconfirmedNetAmountCount);
        Assert.Null(result.OrderSettlements.Last().GrossAmount);
    }

    [Fact]
    public async Task 명시공제0과미확정null을구별하고_저장된모의실패와성공은실입금이아니다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.RecordAsync(Start, 3000m);
        var second = await fixture.RecordAsync(Start.AddMinutes(1), 5000m);
        first.공제액 = 0m; first.수령액 = 3000m; first.지급상태Code = 음식주문기사지급상태Code.모의성공;
        second.공제액 = 100m; second.수령액 = 4900m; second.지급상태Code = 음식주문기사지급상태Code.모의실패;
        first.실행모드Code = second.실행모드Code = "Simulation";
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(100m, result.DeductionAmountTotal);
        Assert.Equal(7900m, result.NetAmountTotal);
        Assert.Equal(1, result.SimulationSucceededOrderCount);
        Assert.Equal(1, result.SimulationFailedOrderCount);
        Assert.Equal(3000m, result.SimulationSucceededNetAmountTotal);
        Assert.Null(result.ActualTransferAmountTotal);
        Assert.All(result.OrderSettlements, x => Assert.False(x.IsActualTransferCompleted));
        second.공제액 = null; second.수령액 = null;
        await fixture.Context.SaveChangesAsync();
        var unknown = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Null(unknown.DeductionAmountTotal);
        Assert.Null(unknown.NetAmountTotal);
    }

    [Fact]
    public async Task 완료재시도와여러모의검증이력은주문대금을중복합산하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(Start, 4720m);
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        await fixture.Context.SaveChangesAsync();
        await 음식주문기사정산Recorder.완료기록Async(fixture.Context, order, transport, attempt, default);
        var item = await fixture.Context.음식주문기사정산.SingleAsync();
        item.지급검증목록.Add(new 음식주문기사지급검증 { 지급StableId = "failed-proof", 멱등키 = "failed", 결과Code = "Failed", 검증시각Utc = Start.AddHours(1) });
        item.지급검증목록.Add(new 음식주문기사지급검증 { 지급StableId = "success-proof", 멱등키 = "success", 결과Code = "Succeeded", 검증시각Utc = Start.AddHours(2) });
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(1, result.CompletedOrderCount);
        Assert.Equal(4720m, result.GrossAmountTotal);
        Assert.Equal(2, Assert.Single(result.OrderSettlements).SimulationPayments.Count);
        Assert.Equal("success-proof", Assert.Single(result.OrderSettlements).SimulationPaymentId);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Interrupted")]
    [InlineData("AnotherDriver")]
    public async Task 취소중단다른기사연결은완료정산으로기록되지않고당일집계에도들어가지않는다(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        var (order, transport, attempt) = await fixture.SeedAsync(Start, 4720m);
        if (scenario == "Cancelled") order.상태 = 음식주문상태코드.취소;
        if (scenario == "Interrupted") attempt.중단시각Utc = Start.AddMinutes(-1);
        if (scenario == "AnotherDriver") transport.확정기사Id = "different-driver";
        await Assert.ThrowsAsync<InvalidOperationException>(() => 음식주문기사정산Recorder.완료기록Async(
            fixture.Context, order, transport, attempt, default));
        var result = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(0, result.CompletedOrderCount);
        Assert.Empty(result.OrderSettlements);
    }

    [Fact]
    public async Task 기사API는임의기사ID를받지않고인증기사만조회하며비인증은401이다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.RecordAsync(Start, 3000m);
        await fixture.RecordAsync(Start, 9999m, "other-driver");
        var controller = new 음식배달기사업무Controller(fixture.UseCase(), null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        Assert.IsType<UnauthorizedResult>(await controller.당일정산조회("2026-10-03", default));
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "driver-1"), new Claim(ClaimTypes.Role, 역할명.기사)
        ], "fixture"));
        var response = Assert.IsType<OkObjectResult>(await controller.당일정산조회("2026-10-03", default));
        var result = Assert.IsType<FoodDeliveryDailySettlementDto>(response.Value);
        Assert.Equal("driver-1", result.DriverId);
        Assert.Equal(3000m, result.GrossAmountTotal);
        Assert.Equal(역할명.기사, typeof(음식배달기사업무Controller).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.DoesNotContain(typeof(음식배달기사업무Controller).GetMethod(nameof(음식배달기사업무Controller.당일정산조회))!.GetParameters(),
            x => x.Name!.Contains("driver", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("2026/10/03")]
    [InlineData("2026-02-30")]
    [InlineData("0001-01-01")]
    [InlineData("9999-12-31")]
    public async Task 잘못된날짜는400이고다른날짜로조용히대체하지않는다(string date)
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new 음식배달기사업무Controller(fixture.UseCase(), null!, null!, null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "driver-1")
            ], "fixture")) }
        };
        Assert.IsType<BadRequestObjectResult>(await controller.당일정산조회(date, default));
    }

    [Fact]
    public async Task 빈날은실제입금을0으로확정하지않으며조회는원장을수정하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.RecordAsync(Start, 3000m);
        var original = JsonSerializer.Serialize(item);
        var empty = await fixture.UseCase().GetDailySettlementAsync("driver-1", Day.AddDays(1), default);
        await fixture.UseCase().GetDailySettlementAsync("driver-1", Day, default);
        Assert.Equal(0, empty.CompletedOrderCount);
        Assert.Equal(0m, empty.GrossAmountTotal);
        Assert.Null(empty.ActualTransferAmountTotal);
        Assert.Equal(original, JsonSerializer.Serialize(item));
        Assert.All(fixture.Context.ChangeTracker.Entries(), x => Assert.Equal(EntityState.Unchanged, x.State));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private int _sequence;
        public SsalddelContext Context { get; }
        private Fixture(SqliteConnection connection)
        {
            _connection = connection;
            Context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var fixture = new Fixture(connection);
            await fixture.Context.Database.EnsureCreatedAsync();
            return fixture;
        }
        public FoodDeliveryDriverWorkspaceUseCase UseCase(DateTimeOffset? now = null)
            => new(Context, new DriverWork(), new Monthly(), new Execution(), new Flags(),
                new Clock(now ?? new DateTimeOffset(2026, 10, 3, 2, 0, 0, TimeSpan.Zero)));
        public async Task<음식주문기사정산> RecordAsync(DateTime completed, decimal gross, string driver = "driver-1", bool received = true)
        {
            var (order, transport, attempt) = await SeedAsync(completed, gross, driver, received);
            await 음식주문기사정산Recorder.완료기록Async(Context, order, transport, attempt, default);
            await Context.SaveChangesAsync();
            return await Context.음식주문기사정산.SingleAsync(x => x.주문번호 == order.주문번호);
        }
        public async Task<(음식주문 Order, 운송원장 Transport, 음식배달시도 Attempt)> SeedAsync(
            DateTime completed, decimal gross, string driver = "driver-1", bool received = true)
        {
            var key = (++_sequence).ToString();
            var order = new 음식주문
            {
                주문번호 = $"daily-order-{key}", 주문자UserId = "synthetic-orderer", 음식점명 = "검증 음식점",
                상태 = received ? 음식주문상태코드.수령확인 : 음식주문상태코드.전달완료, UpdatedAt = completed
            };
            var transport = new 운송원장
            {
                의뢰Id = $"daily-offer-{key}", 운송번호 = $"daily-transport-{key}",
                원본의뢰Id = order.주문번호, 원본의뢰유형 = "음식점주문",
                배차업무유형 = 상태값.배차업무유형.음식배달, 확정기사Id = driver,
                기사_운송자 = driver, 배차큐단계 = 상태값.배차큐단계.종료, 기사지급예정액 = gross,
                기사제안요금정책판본 = "frozen.daily.r1", 기사제안요금판정시각Utc = completed.AddMinutes(-20),
                기사제안요금계산근거Json = JsonSerializer.Serialize(new { 요금 = new { 기사지급예정액 = gross }, 정책판본 = "frozen.daily.r1", 기사Id = driver })
            };
            var attempt = new 음식배달시도
            {
                시도StableId = $"daily-attempt-{key}", 주문번호 = order.주문번호, 제안Id = transport.의뢰Id,
                기사Id = driver, 시도순번 = 1, 상태Code = 음식배달시도상태Code.전달완료,
                수락시각Utc = completed.AddMinutes(-20), 전달완료시각Utc = completed
            };
            Context.음식주문.Add(order); Context.운송원장.Add(transport); Context.음식배달시도.Add(attempt);
            await Context.SaveChangesAsync();
            return (order, transport, attempt);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Execution : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation;
        public bool IsSimulation => true; public bool IsOperational => false;
    }
    private sealed class Flags : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => false;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }
    private sealed class Monthly : I배달기사월정산UseCase
    {
        public Task<Result<배달기사월정산응답>> 당월조회Async(string driverId, string? currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok(new 배달기사월정산응답 { 기사Id = driverId }));
        public Task<Result<배달기사월정산결제완료응답>> 결제완료처리Async(string driverId, int year, int month, string? currentUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
    private sealed class DriverWork : I음식배달기사업무Service
    {
        public Task<IReadOnlyList<DriverWorkOfferDto>> 제안조회Async(string driverId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DriverWorkOfferDto>>([]);
        public Task<Result<FoodDeliveryDriverActionResponse>> 수락Async(string driverId, string offerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 묶음수락Async(string driverId, IReadOnlyList<string> offerIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 거절Async(string driverId, string offerId, string? reasonCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 픽업완료Async(string driverId, string offerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 가게도착Async(string driverId, string offerId, 음식배달가게도착요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 중단Async(string driverId, string offerId, 음식배달중단요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<FoodDeliveryDriverActionResponse>> 전달완료Async(string driverId, string offerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
