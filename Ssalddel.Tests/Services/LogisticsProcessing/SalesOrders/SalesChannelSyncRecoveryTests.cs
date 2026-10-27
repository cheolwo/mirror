using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.판매;

namespace Ssalddel.Tests.Services.LogisticsProcessing.SalesOrders;

public sealed class SalesChannelSyncRecoveryTests
{
    [Fact]
    public async Task 일부품목연결실패는_주문전체예약을막고_커서와비공개진단을보존한다()
    {
        await using var f = await Fixture.Create();
        await f.AddProduct(10, "sku-a");
        f.Feed.Orders = [Order([new("product-a", "sku-a", "합성 A", 2), new("unknown", "unknown-sku", "수집 원문 미보관", 1)])];
        var result = await f.Sync();
        Assert.Equal(0, f.Engine.Calls); Assert.Equal(0, result.CreatedOutboundCount);
        Assert.Empty(await f.Db.출고예정.ToArrayAsync());
        Assert.Equal(10, (await f.Db.입고상품.AsNoTracking().SingleAsync()).가용수량);
        Assert.Equal(Fixture.Cursor, (await f.Account()).마지막동기화일시);
        var own = await f.Read("seller-a"); var issue = Assert.Single(own.SyncIssues);
        Assert.Equal(SalesChannelSyncIssueCodes.ProductMappingMissing, issue.IssueCode);
        Assert.Equal(64, issue.OrderKey.Length);
        Assert.DoesNotContain("unknown-sku", (await f.Account()).동기화진단Json);
        Assert.DoesNotContain("고객 비공개 주소", (await f.Account()).동기화진단Json);
        Assert.DoesNotContain("고객 이름", (await f.Account()).동기화진단Json);
        Assert.Empty((await f.Read("seller-b")).SyncIssues);
    }

    [Fact]
    public async Task 연결보완후_같은주문은한번만예약되고_이전보류관측은해결상태로남는다()
    {
        await using var f = await Fixture.Create(); await f.AddProduct(10, "sku-a");
        f.Feed.Orders = [Order([new("product-a", "sku-a", "A", 2), new("product-b", "sku-b", "B", 1)])];
        await f.Sync(); await f.AddProduct(20, "sku-b");
        Assert.Equal(2, (await f.Sync()).CreatedOutboundCount);
        Assert.Equal(0, (await f.Sync()).CreatedOutboundCount);
        Assert.Equal(2, await f.Db.출고예정.CountAsync());
        var stocks = await f.Db.입고상품.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync();
        Assert.Equal(8, stocks[0].가용수량); Assert.Equal(2, stocks[0].예약수량);
        Assert.Equal(9, stocks[1].가용수량); Assert.Equal(1, stocks[1].예약수량);
        Assert.Empty((await f.Read("seller-a")).SyncIssues);
        Assert.Contains("Resolved", (await f.Account()).동기화진단Json);
        Assert.Contains(SalesChannelSyncIssueCodes.ProductMappingMissing, (await f.Account()).동기화진단Json);
    }

    [Fact]
    public async Task 자리표시자수집기는_정상주문0건으로커서를진전시키지않는다()
    {
        await using var f = await Fixture.Create();
        await f.Service([new EmptySalesChannelOrderFeedClient()]).SyncAsync(CommerceChannelOrderSyncScopes.Domestic, default);
        Assert.Equal(Fixture.Cursor, (await f.Account()).마지막동기화일시);
        Assert.Equal(SalesChannelSyncIssueCodes.ProviderUnavailable, Assert.Single((await f.Read("seller-a")).SyncIssues).IssueCode);
    }

    [Fact]
    public async Task 외부조회실패는_원문이나토큰없이표준코드만보관하고_취소는전파한다()
    {
        await using var f = await Fixture.Create();
        f.Feed.Error = new HttpRequestException("secret-token=do-not-store, 고객 비공개 주소");
        await f.Sync();
        Assert.Equal(SalesChannelSyncIssueCodes.FeedReadFailed, Assert.Single((await f.Read("seller-a")).SyncIssues).IssueCode);
        var json = (await f.Account()).동기화진단Json!;
        Assert.DoesNotContain("secret-token", json); Assert.DoesNotContain("고객 비공개 주소", json);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service([f.Feed]).SyncAsync(CommerceChannelOrderSyncScopes.Domestic, cts.Token));
    }

    [Fact]
    public async Task 재고배정미완료는_출고와예약을만들지않고_보완대기로보존한다()
    {
        await using var f = await Fixture.Create(); await f.AddProduct(10, "sku-a");
        f.Feed.Orders = [Order([new("product-a", "sku-a", "A", 11)])]; f.Engine.Complete = false;
        await f.Sync();
        Assert.Empty(await f.Db.출고예정.ToArrayAsync());
        Assert.Equal(0, (await f.Db.입고상품.AsNoTracking().SingleAsync()).예약수량);
        Assert.Equal(SalesChannelSyncIssueCodes.AllocationIncomplete, Assert.Single((await f.Read("seller-a")).SyncIssues).IssueCode);
        Assert.Equal(Fixture.Cursor, (await f.Account()).마지막동기화일시);
    }

    [Fact]
    public async Task 계정진단컬럼은_기존개인정보보호변환을사용한다()
    {
        await using var f = await Fixture.Create();
        await f.Service([]).SyncAsync(CommerceChannelOrderSyncScopes.Domestic, default);
        await using var command = f.Connection.CreateCommand(); command.CommandText = "SELECT 동기화진단_json FROM 판매채널계정 WHERE id = 1";
        var raw = (string)(await command.ExecuteScalarAsync())!;
        Assert.StartsWith("test-protected:", raw);
        Assert.DoesNotContain(SalesChannelSyncIssueCodes.ProviderUnavailable, raw);
        Assert.Contains(SalesChannelSyncIssueCodes.ProviderUnavailable, (await f.Account()).동기화진단Json);
    }

    private static SalesChannelOrderFeedEntry Order(IReadOnlyList<SalesChannelOrderFeedItem> items)
        => new(CommerceChannelKeys.SmartStore, "private-order-001", "고객 이름", "수령인 이름", "고객 비공개 주소", DateTime.UtcNow, items);

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db, ServiceProvider services) : IAsyncDisposable
    {
        internal static readonly DateTime Cursor = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        internal SqliteConnection Connection => connection;
        internal SsalddelContext Db => db;
        internal readonly Feed Feed = new(); internal readonly Engine Engine = new();
        internal SalesChannelOrderSyncService Service(IEnumerable<ISalesChannelOrderFeedClient> feeds)
            => new(db, feeds, Engine, Options.Create(new SalesChannelOrderSyncOptions()), NullLogger<SalesChannelOrderSyncService>.Instance);
        internal Task<SalesChannelOrderSyncResult> Sync() => Service([Feed]).SyncAsync(CommerceChannelOrderSyncScopes.Domestic, default);
        internal Task<판매채널계정> Account() => db.판매채널계정.AsNoTracking().SingleAsync();
        internal Task<판매채널주문목록응답> Read(string user) => new SalesChannelOrderReadService(db, new Current(user, "판매자")).QueryAsync(new(), default);
        internal static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(services).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            db.판매채널계정.Add(new() { Id = 1, UserId = "seller-a", 채널종류 = CommerceChannelKeys.SmartStore, 상점명 = "합성 상점", 마지막동기화일시 = Cursor });
            await db.SaveChangesAsync(); return new(connection, db, services);
        }
        internal async Task AddProduct(long id, string sku)
        {
            db.입고상품.Add(new() { Id = id, 창고Id = 11, 소유자UserId = "seller-a", 판매자UserId = "seller-a", SKU = sku, 상품명 = "합성 상품", 가용수량 = 10, 입고수량 = 10, 계약유형 = 입고계약유형코드.위탁판매 });
            db.판매상품.Add(new() { Id = id, 입고상품Id = id, 소유자UserId = "seller-a", 판매SKU = sku, 대표상품명 = "합성 상품" });
            db.채널출품.Add(new() { Id = id, 판매상품Id = id, 판매채널계정Id = 1, 채널상품번호 = "product-" + sku[^1] });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        public async ValueTask DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); await services.DisposeAsync(); }
    }
    private sealed class Feed : ISalesChannelOrderFeedClient
    {
        internal IReadOnlyList<SalesChannelOrderFeedEntry> Orders = [];
        internal Exception? Error;
        public bool CanFetch(string channelType, string syncScope) => true;
        public Task<IReadOnlyList<SalesChannelOrderFeedEntry>> FetchOrdersAsync(판매채널계정 account, DateTime? sinceUtc, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Error is { } error ? Task.FromException<IReadOnlyList<SalesChannelOrderFeedEntry>>(error) : Task.FromResult(Orders); }
    }
    private sealed class Engine : IOutboundBatchEngine
    {
        internal int Calls; internal bool Complete = true;
        public Task<OutboundBatchPlanResult> PlanAsync(OutboundBatchPlanRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new OutboundBatchPlanResult { IsComplete = Complete,
                Allocations = Complete ? request.Lines.Select(x => new OutboundBatchAllocation { LineKey = x.LineKey, SalesProductId = x.SalesProductId,
                    InboundProductId = x.PreferredInboundProductId!.Value, WarehouseId = 11, Sku = x.Sku, ProductName = x.ProductName, Quantity = x.Quantity }).ToArray() : [] });
        }
    }
    private sealed record Current(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "test-protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value[15..]));
    }
}
