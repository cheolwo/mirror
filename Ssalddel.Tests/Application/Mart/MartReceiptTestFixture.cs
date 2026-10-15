using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Operations;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Mart;

/// <summary>격리 SQL에서 실제 창고/기사/수령 명령을 재사용합니다. 운영·외부 저장·인증 HTTP 시험이 아닙니다.</summary>
internal sealed class MartReceiptTestFixture(SqliteConnection connection, ServiceProvider provider,
    DbContextOptions<SsalddelContext> options, SsalddelContext db) : IAsyncDisposable
{
    public const string GoodsOwner = "r68-goods-owner";
    public const string Seller = "r68-seller";
    public const string Worker = "r68-worker";
    public const string Driver = "r68-driver";
    public const string Buyer = "r68-buyer";
    public SsalddelContext Db { get; } = db;
    public ReceiptClock Clock { get; } = new();
    public SsalddelContext CreateContext() => new(options, new ProtectedData());
    public 마트수령확인UseCase CustomerUseCase(string actor = Buyer, SsalddelContext? context = null, bool enabled = true)
        => new(context ?? Db, new Actor(actor, 역할명.커뮤니티회원), new Gate(enabled), Clock);

    public static async Task<MartReceiptTestFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var provider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(provider).Options;
        var db = new SsalddelContext(options, new ProtectedData()); await db.Database.EnsureCreatedAsync();
        var f = new MartReceiptTestFixture(connection, provider, options, db);
        db.창고.Add(new 창고 { Id = 11, 소유자UserId = Worker, 창고명 = "격리 마트 창고" });
        foreach (var (orderId, quantity, itemId, planId, pickId, packId) in new[] { (701L, 2, 1L, 201L, 901L, 903L), (702L, 3, 2L, 202L, 902L, 904L) })
        {
            var reference = "MART-R68-" + orderId; var sku = "R68-" + itemId;
            db.입고상품.Add(new 입고상품 { Id = itemId, 창고Id = 11, 소유자UserId = GoodsOwner, 판매자UserId = Seller,
                상품명 = "검증 상품 " + itemId, SKU = sku, 가용수량 = quantity, 입고수량 = quantity, 상태 = "적재완료", 보관위치 = "RACK-1" });
            db.출고예정.Add(new 출고예정 { Id = planId, 주문참조번호 = reference, 운송의뢰Id = reference, 출고창고Id = 11,
                입고상품Id = itemId, 판매자UserId = Seller, 주문자UserId = Buyer, 상품명 = "검증 상품 " + itemId, SKU = sku,
                수량 = quantity, 상태 = 출고상태.준비중 });
            db.마트주문.Add(new 마트주문 { Id = orderId, 주문참조번호 = reference, 판매자UserId = Seller, 주문자UserId = Buyer,
                상품목록 = [new() { Id = orderId, 출고예정Id = planId, 상품명 = "검증 상품 " + itemId, SKU = sku, 수량 = quantity }] });
            foreach (var (taskId, kind) in new[] { (pickId, 피킹포장작업유형.피킹), (packId, 피킹포장작업유형.포장) })
                db.피킹포장작업.Add(new 피킹포장작업 { Id = taskId, 작업Key = kind + "-" + orderId, 라인Key = "LINE-" + orderId,
                    작업유형 = kind, 상태 = 피킹포장작업상태.대기, 출고예정Id = planId, 입고상품Id = itemId, 창고Id = 11,
                    주문참조번호 = reference, 상품명 = "검증 상품 " + itemId, SKU = sku, 수량 = quantity,
                    작업자UserId = Worker, 적재대코드 = "RACK-1" });
            db.운송원장.Add(new 운송원장 { 의뢰Id = reference, 원본의뢰Id = reference, 원본의뢰유형 = "SsalddelMartPackedOrder",
                화주Id = Seller, 배차업무유형 = 상태값.배차업무유형.음식배달, 상태 = 상태값.배차대기상태.대기,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = Driver, 추천라운드 = 1, 추천만료시각 = f.Clock.Now.AddHours(2),
                CreatedAt = f.Clock.Now, UpdatedAt = f.Clock.Now });
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return f;
    }

    public async Task CompleteWarehouseAsync()
    {
        var pick = new 피킹작업UseCase(Db, new Actor(Worker, 역할명.창고관리자), new Log(), new Publisher());
        var pack = new 포장작업UseCase(Db, new Actor(Worker, 역할명.창고관리자), new Log(), new Publisher());
        foreach (var orderId in new[] { 701L, 702L })
        {
            var task = await Db.피킹포장작업.AsNoTracking().SingleAsync(row => row.작업Key == 피킹포장작업유형.피킹 + "-" + orderId);
            Assert.True((await pick.시작Async(task.작업Key, Context(), default)).IsSuccess);
            Assert.True((await pick.완료Async(task.작업Key, new() { ProductConfirmed = true, QuantityConfirmed = true, RackCode = "RACK-1" }, Context(), default)).IsSuccess);
            var result = await pack.완료Async(task.입고상품Id!.Value, new() { PackagingQuantity = task.수량,
                PackagingType = 포장유형코드.일반포장, InventoryConfirmed = true, PackageLabelConfirmed = true }, Context(), default);
            Assert.True(result.IsSuccess, string.Join(",", result.Errors));
        }
        Clock.Now = DateTime.UtcNow.AddSeconds(1); Db.ChangeTracker.Clear();
    }

    public async Task DeliverAsync(long orderId)
    {
        var reference = "MART-R68-" + orderId;
        var queue = await Db.운송원장.SingleAsync(row => row.의뢰Id == reference);
        queue.추천만료시각 = Clock.Now.AddHours(1); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
        var driver = DriverService(); var offered = await driver.업무조회Async(Driver, reference);
        Assert.True(offered.IsSuccess, string.Join(",", offered.Errors));
        var accepted = await driver.수락Async(Driver, reference, new() { ClientRequestId = Guid.NewGuid(),
            ExpectedRecommendationRound = offered.Value.RecommendationRound, ExpectedQueueUpdatedAtUtc = offered.Value.QueueUpdatedAtUtc });
        Assert.True(accepted.IsSuccess, string.Join(",", accepted.Errors));
        var slip = await DriverService(Worker, 역할명.창고관리자).전표발급Async(reference, new() { ClientRequestId = Guid.NewGuid() });
        Assert.True(slip.IsSuccess, string.Join(",", slip.Errors)); Clock.Advance(TimeSpan.FromSeconds(1));
        var picked = await driver.픽업Async(Driver, reference, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Value.Revision,
            ExpectedLabelRevision = slip.Value.Revision, Code = slip.Value.QrCode });
        Assert.True(picked.IsSuccess, string.Join(",", picked.Errors)); Clock.Advance(TimeSpan.FromSeconds(1));
        var delivered = await driver.전달Async(Driver, reference, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = picked.Value.Revision });
        Assert.True(delivered.IsSuccess, string.Join(",", delivered.Errors)); Clock.Advance(TimeSpan.FromSeconds(1)); Db.ChangeTracker.Clear();
    }
    public async Task CompleteAllDeliveriesAsync() { await DeliverAsync(701); await DeliverAsync(702); }
    public async Task<마트수령확인요청> ConfirmationRequestAsync(long orderId)
    {
        var attempt = await Db.Set<마트배송시도>().AsNoTracking().SingleAsync(row => row.주문참조번호 == "MART-R68-" + orderId);
        return new() { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = attempt.시도StableId,
            ExpectedRevision = attempt.Revision, FullReceiptConfirmed = true };
    }
    public async Task<string> RawEvidenceAsync(long orderId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT customer_receipt_evidence_json FROM 마트배송시도 WHERE 주문참조번호 = $reference";
        command.Parameters.AddWithValue("$reference", "MART-R68-" + orderId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private 마트기사업무Service DriverService(string actor = Driver, string role = 역할명.기사)
        => new(Db, new Actor(actor, role), new Gate(), new 마트라스트마일원천Adapter(Db), new ProtectedData(), new TestDispatchAvailability(), Clock);
    private static 창고작업요청Context Context()
        => new("WarehouseManagerApp", Worker, "격리 작업자", 역할명.창고관리자, "/work/outbound", "r68", "", "");
    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); await provider.DisposeAsync(); }
    internal sealed class ReceiptClock : TimeProvider
    {
        public DateTime Now { get; set; } = DateTime.UtcNow.AddHours(-1);
        public bool AdvanceOnRead { get; set; }
        public void Advance(TimeSpan value) => Now += value;
        public override DateTimeOffset GetUtcNow() { var value = Now; if (AdvanceOnRead) Now = Now.AddMilliseconds(1); return new(value); }
    }
    private sealed record Actor(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Gate(bool enabled = true) : I마트기사업무Gate { public bool Enabled => enabled; }
    private sealed class ProtectedData : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "r68-protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value[14..]));
    }
    private sealed class Log : I사용자행위로그Service
    { public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
