using System.Data.Common;
using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Food;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Application.Shipper.Payment.Handlers;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Payments;

/// <summary>실제 관계형 트랜잭션/승인 Handler/Event/조회. 외부 PG만 대역이며 HTTP·MySQL·실기기 증거는 아니다.</summary>
public sealed class 음식주문결제승인OutboxServiceTests
{
    [Fact]
    public async Task 모의PG승인부터_Outbox_Event_음식주문_두역할재조회까지_연결된다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db);
        var provider = new TestPaymentProvider();
        var approve = 승인Handler(db, provider);

        Assert.True((await approve.Handle(new("test-key", payment.OrderId, 18000), default)).IsSuccess);
        Assert.True((await approve.Handle(new("test-key", payment.OrderId, 18000), default)).IsSuccess);
        Assert.Equal(1, provider.Calls);
        Assert.Single(await db.결제승인완료Outbox.ToListAsync());

        using var services = new ServiceCollection().AddLogging()
            .AddSingleton(db)
            .AddTransient<INotificationHandler<결제승인완료Event>, 음식주문결제승인완료EventHandler>()
            .BuildServiceProvider();
        var publisher = new Mediator(services);
        var paymentOutbox = new 결제승인완료OutboxService(db, publisher, NullLogger<결제승인완료OutboxService>.Instance);
        Assert.Equal(1, await paymentOutbox.대기이벤트발행Async());
        Assert.Equal("Succeeded", (await db.결제승인완료Outbox.SingleAsync()).처리상태);
        Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
        Assert.Equal(1, await Consumer(db, fixture.Clock).대기승인반영Async());

        // 재시작처럼 별도 DbContext로 정본을 다시 조회한다. tracked entity로 성공을 가장하지 않는다.
        await using var read = fixture.Context();
        var restaurantQuery = new 음식점음식주문조회UseCase(new EfSsalddelFoodOrderStore(read));
        var restaurant = restaurantQuery.상세("sample-food-1", 1)!;
        var restaurantInbox = restaurantQuery.목록(new(), 1);
        Assert.Equal("sample-payment-1", Assert.Single(restaurantInbox.Items).결제승인!.결제Id);
        Assert.Null(restaurantQuery.상세("sample-food-1", 2));
        var orderer = await new 주문자음식주문조회UseCase(read).상세Async("sample-food-1", "sample-orderer", default);
        Assert.True(orderer.IsSuccess);
        Assert.Equal("sample-payment-1", restaurant.결제승인!.결제Id);
        Assert.Equal(18000m, restaurant.결제승인.승인금액);
        Assert.Equal("KRW", restaurant.결제승인.통화);
        Assert.Equal(restaurant.결제승인.승인시각Utc, orderer.Value.결제승인!.승인시각Utc);
        Assert.Equal(restaurant.결제승인.결제Id, orderer.Value.결제승인.결제Id);
        Assert.NotSame(restaurant.결제승인, restaurantInbox.Items[0].결제승인);
        Assert.Equal("주문대기", restaurant.상태);
        Assert.Equal("미요청", restaurant.배차상태);
        Assert.Equal(0, restaurant.Revision);
        Assert.Empty(await read.음식주문상태이력.ToListAsync());
        Assert.Equal("Succeeded", (await read.Command알림Outbox.SingleAsync()).Status);
        Assert.True((await new 주문자음식주문조회UseCase(read).상세Async("sample-food-1", "other-user", default)).IsFailed);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(restaurant.결제승인));
        Assert.Equal(new[] { "결제Id", "승인금액", "승인시각Utc", "통화" }.OrderBy(x => x),
            json.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x));
        Assert.DoesNotContain("test-key", json.RootElement.GetRawText());
        Assert.DoesNotContain("raw-provider-response", json.RootElement.GetRawText());
    }

    [Fact]
    public async Task PG실패는_승인의도를만들지않고_재시도성공후에만_주문에반영된다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db);
        var provider = new TestPaymentProvider { Success = false };
        var handler = 승인Handler(db, provider);
        Assert.True((await handler.Handle(new("test-key", payment.OrderId, 18000), default)).IsFailed);
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
        Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
        provider.Success = true;
        Assert.True((await handler.Handle(new("test-key", payment.OrderId, 18000), default)).IsSuccess);
        await EnqueueAsync(db, payment);
        await Consumer(db, fixture.Clock).대기승인반영Async();
        Assert.Equal(payment.결제Id, (await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
    }

    [Fact]
    public async Task 중복Event와_재시작재처리는_승인한번만반영하고_업무상태를바꾸지않는다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        DateTime firstUpdatedAt;
        await using (var db = fixture.Context())
        {
            var payment = await SeedAsync(db, approved: true);
            await EnqueueAsync(db, payment);
            await EnqueueAsync(db, payment);
            Assert.Equal(2, await Consumer(db, fixture.Clock).대기승인반영Async());
            firstUpdatedAt = (await db.음식주문.AsNoTracking().SingleAsync()).UpdatedAt;
            await EnqueueAsync(db, payment);
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await using var restarted = fixture.Context();
        Assert.Equal(1, await Consumer(restarted, fixture.Clock).대기승인반영Async());
        Assert.Equal(0, await Consumer(restarted, fixture.Clock).대기승인반영Async());
        Assert.Equal(firstUpdatedAt, (await restarted.음식주문.AsNoTracking().SingleAsync()).UpdatedAt);
        Assert.All(await restarted.Command알림Outbox.ToListAsync(), item => Assert.Equal("Succeeded", item.Status));
        Assert.Equal(0, new EfSsalddelFoodOrderStore(restarted).GetOrder("sample-food-1")!.Revision);
    }

    [Theory]
    [InlineData("payment-amount")]
    [InlineData("order-amount")]
    [InlineData("currency")]
    [InlineData("owner")]
    [InlineData("target")]
    [InlineData("target-type")]
    [InlineData("timestamp")]
    [InlineData("not-approved")]
    [InlineData("refunded")]
    [InlineData("legacy-cancelled")]
    [InlineData("cancelled-at")]
    [InlineData("cancelled-order")]
    [InlineData("rejected-order")]
    [InlineData("missing-payment")]
    [InlineData("missing-order")]
    public async Task 정본불일치나_종료주문은_반영하지않는다(string mismatch)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        var order = await db.음식주문.SingleAsync();
        switch (mismatch)
        {
            case "payment-amount": payment.결제금액++; break;
            case "order-amount": order.총주문금액++; break;
            case "currency": payment.통화 = "USD"; break;
            case "owner": payment.화주Id = "other-user"; break;
            case "target": payment.대상Id = "other-order"; break;
            case "target-type": payment.결제대상유형 = 결제공통정의.결제대상유형.용달운송의뢰; break;
            case "timestamp": payment.승인일시 = payment.승인일시!.Value.AddSeconds(1); break;
            case "not-approved": payment.공통결제상태 = 결제공통정의.결제상태.승인대기; break;
            case "refunded": payment.공통결제상태 = 결제공통정의.결제상태.환불완료; break;
            case "legacy-cancelled": payment.결제상태 = "취소"; break;
            case "cancelled-at": payment.취소일시 = DateTime.UtcNow; break;
            case "cancelled-order": order.상태 = "취소"; break;
            case "rejected-order": order.상태 = "거절"; break;
            case "missing-payment": db.결제.Remove(payment); break;
            case "missing-order": db.음식주문.Remove(order); break;
        }
        await db.SaveChangesAsync();
        await Consumer(db, fixture.Clock).대기승인반영Async();
        Assert.Equal("Failed", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.All(await db.음식주문.AsNoTracking().ToListAsync(), item => Assert.Null(item.결제승인Id));
        Assert.Empty(await db.음식주문상태이력.ToListAsync());
    }

    [Theory]
    [InlineData("{bad-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"결제Id\":\"forged\",\"대상Id\":\"sample-food-1\",\"금액\":18000,\"통화\":\"KRW\",\"승인시각Utc\":\"2026-09-27T00:00:00Z\",\"의도\":\"FoodOrderPaymentApproved\"}")]
    public async Task 원문만있고_정본근거없는의도는_실패한다(string payload)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        var intent = await db.Command알림Outbox.SingleAsync();
        intent.PayloadJson = payload;
        await db.SaveChangesAsync();
        Assert.Equal(1, await Consumer(db, fixture.Clock).대기승인반영Async());
        Assert.Equal("Failed", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
    }

    [Fact]
    public async Task 다른승인은_이미연결된승인을_덮어쓰지않는다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var first = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, first);
        await Consumer(db, fixture.Clock).대기승인반영Async();
        var second = new 결제
        {
            결제Id = "sample-payment-2", OrderId = "sample-pg-2", 대상Id = first.대상Id,
            화주Id = first.화주Id, 결제대상유형 = first.결제대상유형,
            결제금액 = first.결제금액, 통화 = first.통화,
            공통결제상태 = first.공통결제상태, 결제상태 = first.결제상태, 승인일시 = first.승인일시
        };
        db.결제.Add(second);
        await db.SaveChangesAsync();
        await EnqueueAsync(db, second);
        await Consumer(db, fixture.Clock).대기승인반영Async();
        Assert.Equal(first.결제Id, (await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
        Assert.Equal(1, await db.Command알림Outbox.CountAsync(x => x.Status == "Failed"));
        Assert.Equal(1, await db.Command알림Outbox.CountAsync(x => x.Status == "Succeeded"));
    }

    [Fact]
    public async Task 승인저장후_Outbox성공저장실패는_둘다롤백하고_새Context로재시도한다()
    {
        var fault = new FailOutboxCompletionInterceptor { FailuresRemaining = 1 };
        await using var fixture = await DatabaseFixture.CreateAsync(fault);
        await using (var db = fixture.Context())
        {
            var payment = await SeedAsync(db, approved: true);
            await EnqueueAsync(db, payment);
            await Consumer(db, fixture.Clock).대기승인반영Async();
            Assert.Equal(0, fault.FailuresRemaining);
            Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
            var intent = await db.Command알림Outbox.AsNoTracking().SingleAsync();
            Assert.Equal("Pending", intent.Status);
            Assert.Equal(1, intent.RetryCount);
            Assert.Equal(0, await Consumer(db, fixture.Clock).대기승인반영Async());
            // 이후 SaveChanges에도 실패한 승인 값이 추적 객체에서 새어나오지 않는다.
            await db.SaveChangesAsync();
        }

        fixture.Clock.Advance(OutboxProcessingPolicy.RetryDelay + TimeSpan.FromSeconds(1));
        await using var restarted = fixture.Context();
        Assert.Null((await restarted.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
        Assert.Equal(1, await Consumer(restarted, fixture.Clock).대기승인반영Async());
        Assert.Equal("sample-payment-1", (await restarted.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
        var completed = await restarted.Command알림Outbox.AsNoTracking().SingleAsync();
        Assert.Equal("Succeeded", completed.Status);
        Assert.Equal(2, completed.RetryCount);
    }

    [Fact]
    public async Task 중단된처리의_lease가만료된후에만_재선점한다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        var item = await db.Command알림Outbox.SingleAsync();
        item.Status = "Processing";
        item.RetryCount = 1;
        item.UpdatedAt = fixture.Clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        Assert.Equal(0, await Consumer(db, fixture.Clock).대기승인반영Async());
        fixture.Clock.Advance(OutboxProcessingPolicy.LeaseTimeout + TimeSpan.FromSeconds(1));
        Assert.Equal(1, await Consumer(db, fixture.Clock).대기승인반영Async());
        Assert.Equal(2, (await db.Command알림Outbox.AsNoTracking().SingleAsync()).RetryCount);
    }

    [Fact]
    public async Task 최대재시도이후는_실패로남기며_주문승인은남지않는다()
    {
        var fault = new FailOutboxCompletionInterceptor { FailuresRemaining = 1 };
        await using var fixture = await DatabaseFixture.CreateAsync(fault);
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        var item = await db.Command알림Outbox.SingleAsync();
        item.RetryCount = OutboxProcessingPolicy.MaximumAttempts - 1;
        item.UpdatedAt = fixture.Clock.GetUtcNow().UtcDateTime - TimeSpan.FromMinutes(1);
        await db.SaveChangesAsync();
        await Consumer(db, fixture.Clock).대기승인반영Async();
        Assert.Equal("Failed", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
    }

    [Fact]
    public async Task 기본비활성과_다른종류Outbox는_소비하지않는다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        var disabled = new 음식주문결제승인OutboxService(db, Options.Create(new 음식주문결제승인Options()), fixture.Clock,
            NullLogger<음식주문결제승인OutboxService>.Instance);
        Assert.Equal(0, await disabled.대기승인반영Async());
        var item = await db.Command알림Outbox.SingleAsync();
        Assert.Equal(0, item.RetryCount);
        item.Target = "OtherWorkflow";
        await db.SaveChangesAsync();
        Assert.Equal(0, await Consumer(db, fixture.Clock).대기승인반영Async());
        Assert.Equal("Pending", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Null(new EfSsalddelFoodOrderStore(db).GetOrder("sample-food-1")!.결제승인);
    }

    [Fact]
    public async Task 한의도가잘못되어도_다음정상의도는_독립처리된다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        (await db.Command알림Outbox.SingleAsync()).PayloadJson = "{}";
        await db.SaveChangesAsync();
        await EnqueueAsync(db, payment);
        Assert.Equal(2, await Consumer(db, fixture.Clock).대기승인반영Async());
        Assert.Equal(1, await db.Command알림Outbox.CountAsync(x => x.Status == "Failed"));
        Assert.Equal(1, await db.Command알림Outbox.CountAsync(x => x.Status == "Succeeded"));
        Assert.NotNull((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
    }

    [Fact]
    public async Task 취소된주문의_기존승인재전달은_주문을되살리지않는다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        await Consumer(db, fixture.Clock).대기승인반영Async();
        await db.음식주문.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.상태, "취소"));
        await EnqueueAsync(db, payment);
        await Consumer(db, fixture.Clock).대기승인반영Async();
        var order = await db.음식주문.AsNoTracking().SingleAsync();
        Assert.Equal("취소", order.상태);
        Assert.Equal(payment.결제Id, order.결제승인Id);
        Assert.All(await db.Command알림Outbox.AsNoTracking().ToListAsync(), x => Assert.Equal("Succeeded", x.Status));
    }

    [Fact]
    public async Task 취소토큰은_성공으로처리하지않는다()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context();
        var payment = await SeedAsync(db, approved: true);
        await EnqueueAsync(db, payment);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Consumer(db, fixture.Clock).대기승인반영Async(cancellationToken: cancelled.Token));
        Assert.Equal("Pending", (await db.Command알림Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await db.음식주문.AsNoTracking().SingleAsync()).결제승인Id);
    }

    private static 토스결제승인CommandHandler 승인Handler(SsalddelContext db, TestPaymentProvider provider)
        => new(db, new 공통결제Service([provider]), new 콘텐츠혜택계산Service(db), new CurrentUser());

    private static 음식주문결제승인OutboxService Consumer(SsalddelContext db, TimeProvider clock)
        => new(db, Options.Create(new 음식주문결제승인Options { Enabled = true }), clock,
            NullLogger<음식주문결제승인OutboxService>.Instance);

    private static async Task<결제> SeedAsync(SsalddelContext db, bool approved = false)
    {
        db.음식주문.Add(new 음식주문
        {
            주문번호 = "sample-food-1", 음식점Id = 1, 음식점명 = "합성 음식점",
            주문자UserId = "sample-orderer", 총주문금액 = 18000m
        });
        var payment = new 결제
        {
            결제Id = "sample-payment-1", OrderId = "sample-pg-1", 대상Id = "sample-food-1",
            화주Id = "sample-orderer", 결제대상유형 = 결제공통정의.결제대상유형.음식주문,
            결제금액 = 18000, 통화 = "KRW",
            결제상태 = approved ? 상태값.결제상태.결제완료 : 상태값.결제상태.결제대기,
            공통결제상태 = approved ? 결제공통정의.결제상태.승인완료 : 결제공통정의.결제상태.승인대기,
            승인일시 = approved ? new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc) : null
        };
        db.결제.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    private static Task EnqueueAsync(SsalddelContext db, 결제 payment)
        => new 음식주문결제승인완료EventHandler(db, NullLogger<음식주문결제승인완료EventHandler>.Instance)
            .Handle(new 결제승인완료Event(payment.Id, payment.결제Id, payment.결제대상유형,
                payment.대상Id, payment.결제제공자, payment.결제금액, payment.통화, payment.승인일시!.Value), default);

    private sealed class CurrentUser : ICurrentUserAccessor
    {
        public string? UserId => "sample-orderer";
        public string? Role => "Orderer";
    }

    private sealed class TestPaymentProvider : I결제Provider
    {
        public int 제공자유형 => 결제공통정의.결제제공자.TossPayments;
        public bool Success { get; set; } = true;
        public int Calls { get; private set; }
        public Task<결제승인결과> 결제승인Async(결제승인요청 request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new 결제승인결과(Success, "raw-provider-response", "카드", "test-transaction"));
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FailOutboxCompletionInterceptor : DbCommandInterceptor
    {
        public int FailuresRemaining { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailuresRemaining > 0 && command.CommandText.Contains("UPDATE \"Command_알림_Outbox\"", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(p => Equals(p.Value, "Succeeded")))
            {
                FailuresRemaining--;
                throw new InvalidOperationException("Synthetic transaction failure");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DatabaseFixture(SqliteConnection connection, DbContextOptions<SsalddelContext> options) : IAsyncDisposable
    {
        public TestClock Clock { get; } = new();
        public SsalddelContext Context() => new(options, new PassThroughEncryption());
        public static async Task<DatabaseFixture> CreateAsync(DbCommandInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            var fixture = new DatabaseFixture(connection, builder.Options);
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
