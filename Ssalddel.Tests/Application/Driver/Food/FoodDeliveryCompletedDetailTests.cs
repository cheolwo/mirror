using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Progress;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Food;

public sealed class FoodDeliveryCompletedDetailTests
{
    private static readonly DateTime Completed = new(2026, 10, 3, 2, 0, 0, DateTimeKind.Utc);
    private const string Driver = "driver-completed";
    private const string PrivateName = "완료 고객 테스트";
    private const string PrivatePhone = "010-0000-1234";
    private const string PrivateAddress = "서울 테스트 고객로 10";
    private const string PrivateMenu = "비공개 주문 메뉴";

    [Theory]
    [InlineData(운송의뢰배차원천유형.음식점주문)]
    [InlineData(운송의뢰배차원천유형.음식주문)]
    [InlineData("음식점주문")]
    public async Task 실제음식주문원본과기존음식원본은완료연결검증후상세를반환한다(string sourceType)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Transport.원본의뢰유형 = sourceType;
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase(Completed.AddHours(1)).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("Allowed", result.DetailAccessStatusCode);
        Assert.Equal(Completed.AddDays(3), result.DetailExpiresAtUtc);
        Assert.Equal(PrivateName, result.CustomerDetails!.DisplayName);
        Assert.Equal(PrivateMenu, Assert.Single(result.OrderDetails!.Items).MenuName);
        Assert.Equal(4720m, result.Settlement.GrossAmount);
        Assert.Null(result.Settlement.DeductionAmount);
    }

    [Theory]
    [InlineData(운송의뢰배차원천유형.화주운송의뢰, true)]
    [InlineData(운송의뢰배차원천유형.창고출고연계운송, true)]
    [InlineData(운송의뢰배차원천유형.살뜰마트음식주문, true)]
    [InlineData("Unknown", true)]
    [InlineData("", true)]
    [InlineData(운송의뢰배차원천유형.음식점주문, false)]
    public async Task 다른원본이나화물업무는정산소유권이같아도고객상세를허용하지않는다(string sourceType, bool foodDomain)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Transport.원본의뢰유형 = sourceType;
        fixture.Transport.배차업무유형 = foodDomain ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송;
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase(Completed.AddHours(1)).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("CompletionEvidenceUnavailable", result.DetailAccessStatusCode);
        Assert.Equal(4720m, result.Settlement.GrossAmount);
        AssertPrivateDetailsAbsent(result);
    }

    [Theory]
    [InlineData(-1, "Allowed")]
    [InlineData(0, "Expired")]
    [InlineData(1, "Expired")]
    public async Task 사용자확정3일기한은정확한만료시점부터주문고객상세를제외하고정산은유지한다(int seconds, string status)
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.UseCase(Completed.AddDays(3).AddSeconds(seconds)).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);

        Assert.NotNull(result);
        Assert.Equal(status, result.DetailAccessStatusCode);
        Assert.Equal(Completed.AddDays(3), result.DetailExpiresAtUtc);
        Assert.Equal(4720m, result.Settlement.GrossAmount);
        Assert.Null(result.Settlement.DeductionAmount);
        Assert.False(result.Settlement.IsActualTransferCompleted);
        if (status == "Allowed")
        {
            Assert.Equal(PrivateName, result.CustomerDetails!.DisplayName);
            Assert.Equal(PrivatePhone, result.CustomerDetails.ContactPhone);
            Assert.Equal(PrivateAddress + " 301호", result.CustomerDetails.Address);
            Assert.Equal("문 앞에 놓아 주세요", result.CustomerDetails.DeliveryInstructions);
            Assert.Equal(PrivateMenu, Assert.Single(result.OrderDetails!.Items).MenuName);
            Assert.Equal(2, Assert.Single(result.OrderDetails.Items).Quantity);
            Assert.Equal(21000m, result.OrderDetails.TotalOrderAmount);
        }
        else AssertPrivateDetailsAbsent(result);
    }

    [Fact]
    public async Task 다른기사와없는정산과빈인증은모두상세를반환하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var useCase = fixture.UseCase(Completed.AddHours(1));
        Assert.Null(await useCase.GetCompletedDeliveryDetailAsync("another-driver", fixture.Settlement.정산StableId, default));
        Assert.Null(await useCase.GetCompletedDeliveryDetailAsync(Driver, "unknown-settlement", default));
        Assert.Null(await useCase.GetCompletedDeliveryDetailAsync("", fixture.Settlement.정산StableId, default));
        Assert.Null(await useCase.GetCompletedDeliveryDetailAsync(Driver, "", default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 기간설정을명시중지하면정산만보이고민감상세는제공하지않는다(int? window)
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.UseCase(Completed.AddHours(1), window).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("PolicyNotConfigured", result.DetailAccessStatusCode);
        Assert.Null(result.DetailExpiresAtUtc);
        AssertPrivateDetailsAbsent(result);
    }

    [Fact]
    public async Task 운영자기간설정은완료시각기준으로적용하고현재조회시각에서기간을갱신하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.UseCase(Completed.AddHours(2), 60).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("Expired", result.DetailAccessStatusCode);
        Assert.Equal(Completed.AddHours(1), result.DetailExpiresAtUtc);
        AssertPrivateDetailsAbsent(result);
    }

    [Theory]
    [InlineData("MissingCompletion")]
    [InlineData("FutureCompletion")]
    [InlineData("AttemptOwner")]
    [InlineData("AttemptStableId")]
    [InlineData("AttemptTime")]
    [InlineData("Interrupted")]
    [InlineData("OrderLink")]
    [InlineData("TransportOwner")]
    [InlineData("TransportCarrier")]
    [InlineData("TransportNotClosed")]
    [InlineData("TransportSourceOrder")]
    [InlineData("AttemptState")]
    [InlineData("OfferLink")]
    [InlineData("CancelledOrder")]
    public async Task 완료근거나원장연결이잘못되면민감정보를조회에대입하지않는다(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        switch (scenario)
        {
            case "MissingCompletion": fixture.Settlement.전달완료시각Utc = default; break;
            case "FutureCompletion": fixture.Settlement.전달완료시각Utc = Completed.AddDays(2); break;
            case "AttemptOwner": fixture.Attempt.기사Id = "another-driver"; break;
            case "AttemptStableId": fixture.Settlement.배달시도StableId = "wrong-stable-id"; break;
            case "AttemptTime": fixture.Attempt.전달완료시각Utc = Completed.AddMinutes(1); break;
            case "Interrupted": fixture.Attempt.중단시각Utc = Completed.AddMinutes(-1); break;
            case "OrderLink": fixture.Settlement.주문번호 = "wrong-order"; break;
            case "TransportOwner": fixture.Transport.확정기사Id = "another-driver"; break;
            case "TransportCarrier": fixture.Transport.기사_운송자 = "another-driver"; break;
            case "TransportNotClosed": fixture.Transport.배차큐단계 = 상태값.배차큐단계.확정; break;
            case "TransportSourceOrder": fixture.Transport.원본의뢰Id = "wrong-order"; break;
            case "AttemptState": fixture.Attempt.상태Code = 음식배달시도상태Code.픽업완료; break;
            case "OfferLink": fixture.Attempt.제안Id = "wrong-offer"; break;
            case "CancelledOrder": fixture.Order.상태 = 음식주문상태코드.취소; break;
        }
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.UseCase(Completed.AddHours(1)).GetCompletedDeliveryDetailAsync(
            Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("CompletionEvidenceUnavailable", result.DetailAccessStatusCode);
        Assert.Equal(4720m, result.Settlement.GrossAmount);
        AssertPrivateDetailsAbsent(result);
    }

    [Fact]
    public async Task DB조회중만료되면응답생성시점에도다시차단한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var clock = new AdvancingClock(Completed.AddDays(3).AddSeconds(-1), Completed.AddDays(3));
        var result = await fixture.UseCase(clock).GetCompletedDeliveryDetailAsync(Driver, fixture.Settlement.정산StableId, default);
        Assert.NotNull(result);
        Assert.Equal("Expired", result.DetailAccessStatusCode);
        Assert.Equal(Completed.AddDays(3), result.ServerNowUtc);
        AssertPrivateDetailsAbsent(result);
    }

    [Fact]
    public async Task 만료열람은원본개인정보나정산을파기수정하지않고응답에서만제외한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var before = JsonSerializer.Serialize(fixture.Settlement);
        var result = await fixture.UseCase(Completed.AddDays(10)).GetCompletedDeliveryDetailAsync(Driver, fixture.Settlement.정산StableId, default);
        AssertPrivateDetailsAbsent(result!);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Settlement));
        Assert.Equal(PrivateName, (await fixture.Context.음식주문.AsNoTracking().SingleAsync()).수령인명);
        Assert.All(fixture.Context.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public async Task API는인증기사본인만허용하고민감상세성공실패응답모두캐시를금지한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new 음식배달기사업무Controller(fixture.UseCase(Completed.AddHours(1)), null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Assert.IsType<UnauthorizedResult>(await controller.완료배달상세조회(fixture.Settlement.정산StableId, default));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        controller.HttpContext.User = Principal("another-driver");
        Assert.IsType<NotFoundResult>(await controller.완료배달상세조회(fixture.Settlement.정산StableId, default));
        controller.HttpContext.User = Principal(Driver);
        var response = Assert.IsType<OkObjectResult>(await controller.완료배달상세조회(fixture.Settlement.정산StableId, default));
        Assert.NotNull(Assert.IsType<FoodDeliveryCompletedDeliveryDetailDto>(response.Value).CustomerDetails);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task 일반화물조회목록현재상세와공통원장이벤트로음식배달고객정보를우회조회할수없다()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Transport.출발지 = "음식점 전체 주소";
        fixture.Transport.도착지 = PrivateAddress;
        fixture.Transport.첨부_json = JsonSerializer.Serialize(new { RecipientPhone = PrivatePhone });
        fixture.Context.운송이벤트.Add(new 운송이벤트
        {
            의뢰Id = fixture.Transport.운송번호, 이벤트타입 = "음식배달위치감사", 이벤트시각 = Completed,
            메타데이터 = "{\"TargetLatitude\":37.5,\"TargetLongitude\":127.1}"
        });
        await fixture.Context.SaveChangesAsync();
        Assert.Empty(await new 운송목록조회QueryHandler(fixture.Context).Handle(new 운송목록조회Query(Driver), default));
        Assert.Null(await new 운송현재조회QueryHandler(fixture.Context).Handle(new 운송현재조회Query(Driver), default));
        Assert.Null(await new 운송상세조회QueryHandler(fixture.Context).Handle(new 운송상세조회Query(Driver, fixture.Transport.Id), default));
        var events = await new 운송원장이벤트조회QueryHandler(fixture.Context, new CurrentUser(Driver, 역할명.기사), null!)
            .Handle(new 운송원장이벤트조회Query(fixture.Transport.운송번호, null), default);
        Assert.True(events.IsFailed);

        // 실제 화물 상세 동작은 유지합니다.
        fixture.Transport.배차업무유형 = 상태값.배차업무유형.용달운송;
        await fixture.Context.SaveChangesAsync();
        var freight = await new 운송상세조회QueryHandler(fixture.Context).Handle(new 운송상세조회Query(Driver, fixture.Transport.Id), default);
        Assert.NotNull(freight);
        Assert.Equal(PrivateAddress, freight.도착지);
    }

    private static ClaimsPrincipal Principal(string id) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, 역할명.기사)
    ], "completed-detail-test"));
    private static void AssertPrivateDetailsAbsent(FoodDeliveryCompletedDeliveryDetailDto result)
    {
        Assert.Null(result.OrderDetails);
        Assert.Null(result.CustomerDetails);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(PrivateName, json);
        Assert.DoesNotContain(PrivatePhone, json);
        Assert.DoesNotContain(PrivateAddress, json);
        Assert.DoesNotContain(PrivateMenu, json);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public SsalddelContext Context { get; }
        public 음식주문 Order { get; private set; } = null!;
        public 운송원장 Transport { get; private set; } = null!;
        public 음식배달시도 Attempt { get; private set; } = null!;
        public 음식주문기사정산 Settlement { get; private set; } = null!;
        private Fixture(SqliteConnection connection)
        {
            _connection = connection;
            Context = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var fixture = new Fixture(connection);
            await fixture.Context.Database.EnsureCreatedAsync();
            fixture.Order = new 음식주문
            {
                주문번호 = "completed-order", 주문자UserId = "orderer-test", 음식점명 = "완료 음식점",
                음식점주소 = "서울 테스트 음식점로 1", 음식점상세주소 = "1층", 상태 = 음식주문상태코드.수령확인,
                수령인명 = PrivateName, 수령인연락처 = PrivatePhone, 수령지주소 = PrivateAddress,
                수령지상세주소 = "301호", 수령요청사항 = "문 앞에 놓아 주세요", 총주문금액 = 21000m, UpdatedAt = Completed,
                상품목록 = [new 음식주문상품 { 상품명 = PrivateMenu, 수량 = 2, 단가 = 10500m }]
            };
            fixture.Transport = new 운송원장
            {
                의뢰Id = "completed-offer", 운송번호 = "completed-transport", 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
                원본의뢰Id = fixture.Order.주문번호, 기사_운송자 = Driver, 확정기사Id = Driver,
                배차업무유형 = 상태값.배차업무유형.음식배달, 배차큐단계 = 상태값.배차큐단계.종료,
                기사지급예정액 = 4720m, 기사제안요금정책판본 = "completed.frozen.r1", 기사제안요금판정시각Utc = Completed.AddMinutes(-20),
                기사제안요금계산근거Json = JsonSerializer.Serialize(new { 요금 = new { 기사지급예정액 = 4720m }, 정책판본 = "completed.frozen.r1", 기사Id = Driver })
            };
            fixture.Attempt = new 음식배달시도
            {
                시도StableId = "completed-attempt", 주문번호 = fixture.Order.주문번호, 제안Id = fixture.Transport.의뢰Id,
                기사Id = Driver, 시도순번 = 1, 상태Code = 음식배달시도상태Code.전달완료,
                수락시각Utc = Completed.AddMinutes(-20), 전달완료시각Utc = Completed
            };
            fixture.Context.음식주문.Add(fixture.Order); fixture.Context.운송원장.Add(fixture.Transport); fixture.Context.음식배달시도.Add(fixture.Attempt);
            await fixture.Context.SaveChangesAsync();
            await 음식주문기사정산Recorder.완료기록Async(fixture.Context, fixture.Order, fixture.Transport, fixture.Attempt, default);
            await fixture.Context.SaveChangesAsync();
            fixture.Settlement = await fixture.Context.음식주문기사정산.SingleAsync();
            return fixture;
        }
        public FoodDeliveryDriverWorkspaceUseCase UseCase(DateTime now, int? window = 4320) => UseCase(new Clock(now), window);
        public FoodDeliveryDriverWorkspaceUseCase UseCase(TimeProvider clock, int? window = 4320)
            => new(Context, null!, null!, new Execution(), null!, clock,
                Options.Create(new FoodDeliveryCompletedDetailAccessOptions { WindowMinutes = window }));
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class AdvancingClock(DateTime first, DateTime next) : TimeProvider
    {
        private int _readCount;
        public override DateTimeOffset GetUtcNow() => new(_readCount++ == 0 ? first : next);
    }
    private sealed record CurrentUser(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Execution : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation;
        public bool IsSimulation => true;
        public bool IsOperational => false;
    }
}
