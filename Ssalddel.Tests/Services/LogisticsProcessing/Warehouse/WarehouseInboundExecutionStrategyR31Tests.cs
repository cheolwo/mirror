using Microsoft.Data.Sqlite;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class WarehouseInboundExecutionStrategyR31Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수령과검수는_재시도전략의트랜잭션관문안에서정상저장한다(bool inspection)
    {
        await using var fixture = await Fixture.Create(inspection);
        await using var db = fixture.Open(retrying: true);

        await Execute(Service(db), inspection);

        await AssertCommitted(fixture, inspection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장중일시실패는_같은상태부터재시도하고_상품재고이력과알림을한번만남긴다(bool inspection)
    {
        await using var fixture = await Fixture.Create(inspection);
        var fault = new HistorySaveFault(inspection, retryable: true);
        await using var db = fixture.Open(fault, retrying: true);

        await Execute(Service(db), inspection);

        Assert.Equal(1, fault.Failures);
        await AssertCommitted(fixture, inspection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 영구저장실패는_상품과수량과알림을함께롤백하고_새추적객체를남기지않는다(bool inspection)
    {
        await using var fixture = await Fixture.Create(inspection);
        var fault = new HistorySaveFault(inspection, retryable: false);
        await using (var write = fixture.Open(fault, retrying: true))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(Service(write), inspection));
            Assert.Empty(write.ChangeTracker.Entries<재고이력>());
            Assert.Empty(write.ChangeTracker.Entries<재고이동>());
            Assert.Empty(write.ChangeTracker.Entries<살뜰.도메인.설정.Command알림Outbox>());
        }

        Assert.Equal(1, fault.Failures);
        await using var read = fixture.Open();
        var inbound = await read.입고요청.AsNoTracking().SingleAsync();
        Assert.Equal(inspection ? "입고완료" : "입고예정", inbound.상태);
        Assert.Empty(await read.재고이력.ToArrayAsync());
        Assert.Empty(await read.재고이동.ToArrayAsync());
        Assert.Empty(await read.Command알림Outbox.ToArrayAsync());
        if (inspection)
        {
            var item = await read.입고상품.AsNoTracking().SingleAsync();
            Assert.Equal("보관중", item.상태);
            Assert.Equal(2, item.가용수량);
        }
        else
        {
            Assert.Empty(await read.입고상품.ToArrayAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 동일완료재요청은_기존결과를돌려주고_다른수량은거절하여정본을보존한다(bool inspection)
    {
        await using var fixture = await Fixture.Create(inspection);
        await using var db = fixture.Open(retrying: true);
        var service = Service(db);
        await Execute(service, inspection);

        if (inspection)
        {
            Assert.True((await service.InspectInboundItemAsync(31, Inspection(), default)).멱등재시도여부);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.InspectInboundItemAsync(
                31, new() { 검수수량 = 3, 검수메모 = "R31 retry regression" }, default));
        }
        else
        {
            Assert.True((await service.CompleteInboundAsync(21, Receive(), default)).멱등재시도여부);
            var changed = Receive();
            changed.Items[0].입고수량 = 3;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteInboundAsync(21, changed, default));
        }

        await AssertCommitted(fixture, inspection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 호출자가연트랜잭션은_수령검수가커밋하지않고_호출자롤백으로함께취소된다(bool inspection)
    {
        await using var fixture = await Fixture.Create(inspection);
        await using (var db = fixture.Open(retrying: true))
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                await Execute(Service(db), inspection);
                Assert.Same(transaction, db.Database.CurrentTransaction);
                await transaction.RollbackAsync();
                db.ChangeTracker.Clear();
            });
        }

        await using var read = fixture.Open();
        Assert.Equal(inspection ? "입고완료" : "입고예정", (await read.입고요청.SingleAsync()).상태);
        Assert.Empty(await read.재고이력.ToArrayAsync());
        Assert.Empty(await read.재고이동.ToArrayAsync());
        Assert.Empty(await read.Command알림Outbox.ToArrayAsync());
        Assert.Equal(inspection ? 1 : 0, await read.입고상품.CountAsync());
        if (inspection) Assert.Equal("보관중", (await read.입고상품.SingleAsync()).상태);
    }

    [Fact]
    public async Task 수령재시도는_호출전부터대기중인별도변경을지우지않고_재고와한번만저장한다()
    {
        await using var fixture = await Fixture.Create(inspection: false);
        var fault = new HistorySaveFault(inspection: false, retryable: true);
        await using (var write = fixture.Open(fault, retrying: true))
        {
            var warehouse = await write.창고.SingleAsync();
            warehouse.창고명 = "R31 pending change preserved";
            await Execute(Service(write), inspection: false);
        }

        await using var read = fixture.Open();
        Assert.Equal("R31 pending change preserved", (await read.창고.SingleAsync()).창고명);
        Assert.Equal(1, fault.Failures);
        await AssertCommitted(fixture, inspection: false);
    }

    private static WarehouseOperationService Service(SsalddelContext db) => new(db, new User(), null!);
    private static 입고완료요청 Receive() => new()
    {
        Items = [new() { 상품명 = "R31 test box", SKU = "R31-BOX", 입고수량 = 2, 불량수량 = 0 }]
    };
    private static 입고검수요청 Inspection() => new() { 검수수량 = 2, 불량수량 = 0, 검수메모 = "R31 retry regression" };
    private static async Task Execute(WarehouseOperationService service, bool inspection)
    {
        if (inspection) await service.InspectInboundItemAsync(31, Inspection(), default);
        else await service.CompleteInboundAsync(21, Receive(), default);
    }

    private static async Task AssertCommitted(Fixture fixture, bool inspection)
    {
        await using var read = fixture.Open();
        Assert.Equal("입고완료", (await read.입고요청.AsNoTracking().SingleAsync()).상태);
        var item = await read.입고상품.AsNoTracking().SingleAsync();
        Assert.Equal(2, item.입고수량);
        Assert.Equal(2, item.가용수량);
        Assert.Equal(0, item.불량수량);
        Assert.Equal(0, item.예약수량);
        Assert.Equal(inspection ? "검수완료" : "보관중", item.상태);
        var history = Assert.Single(await read.재고이력.AsNoTracking().ToArrayAsync());
        Assert.Equal(inspection ? "입고검수" : "입고", history.이력유형);
        Assert.Equal(inspection ? 0 : 1, await read.재고이동.CountAsync());
        var notices = await read.Command알림Outbox.AsNoTracking().ToArrayAsync();
        Assert.Equal(3, notices.Length);
        Assert.All(notices, notice => Assert.Equal(inspection ? "warehouse.inspected" : "warehouse.received", notice.EventName));
        var payloads = notices.Select(notice => JsonDocument.Parse(notice.PayloadJson)).ToArray();
        try
        {
            Assert.Equal(3, payloads.Select(payload => payload.RootElement.GetProperty("eventId").GetString()).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(new[] { "customer", "seller", "warehouse-owner" }, payloads
                .Select(payload => payload.RootElement.GetProperty("targetUserId").GetString()).Order().ToArray());
        }
        finally { foreach (var payload in payloads) payload.Dispose(); }
    }

    private sealed record User : ICurrentUserAccessor
    {
        public string? UserId => "worker";
        public string? Role => "창고관리자";
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class RetryStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }
    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }
    private sealed class HistorySaveFault(bool inspection, bool retryable) : SaveChangesInterceptor
    {
        public int Failures { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Failures == 0 && eventData.Context?.ChangeTracker.Entries<재고이력>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.이력유형 == (inspection ? "입고검수" : "입고")) == true)
            {
                Failures++;
                if (retryable) throw new TimeoutException("R31 isolated save timeout");
                throw new InvalidOperationException("R31 isolated permanent save failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        // 기본·재시도 provider를 이 fixture 안에서 공유하여 전역 EF provider cache와 분리한다.
        private static readonly ServiceProvider OrdinaryProvider = new ServiceCollection()
            .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);
        private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
            .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, RetryStrategyFactory>()
            .BuildServiceProvider(validateScopes: true);
        private readonly string name = "warehouse-r31-" + Guid.NewGuid().ToString("N");
        private readonly SqliteConnection keeper;
        private Fixture() => keeper = new SqliteConnection(ConnectionString);
        private string ConnectionString => $"Data Source={name};Mode=Memory;Cache=Shared;Default Timeout=2";
        public SsalddelContext Open(IInterceptor? interceptor = null, bool retrying = false)
        {
            var options = new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(ConnectionString)
                .UseInternalServiceProvider(retrying ? RetryingProvider : OrdinaryProvider);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options, new Encryption());
        }
        public static async Task<Fixture> Create(bool inspection)
        {
            var fixture = new Fixture();
            await fixture.keeper.OpenAsync();
            await using var db = fixture.Open();
            await db.Database.EnsureCreatedAsync();
            db.창고.Add(new 창고 { Id = 7, 소유자UserId = "warehouse-owner", 창고명 = "R31 test warehouse" });
            db.창고사용자.Add(new 창고사용자 { 창고Id = 7, UserId = "worker", 역할명 = "출고" });
            db.입고요청.Add(new 입고요청
            {
                Id = 21, 창고Id = 7, 주문자UserId = "customer", 판매자UserId = "seller",
                주문참조번호 = "R31", 상태 = inspection ? "입고완료" : "입고예정"
            });
            if (inspection) db.입고상품.Add(new 입고상품
            {
                Id = 31, 입고요청Id = 21, 창고Id = 7, 소유자UserId = "customer", 판매자UserId = "seller",
                상품명 = "R31 test box", SKU = "R31-BOX", 입고수량 = 2, 가용수량 = 2, 상태 = "보관중"
            });
            await db.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => keeper.DisposeAsync();
    }
}
