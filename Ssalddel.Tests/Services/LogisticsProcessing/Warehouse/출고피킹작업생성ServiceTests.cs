using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class 출고피킹작업생성ServiceTests
{
    [Fact]
    public void 자동작업생성은_명시적으로활성화하기전까지꺼져있다()
    {
        var options = new 출고피킹작업생성Options();

        Assert.False(options.Enabled);
        Assert.Equal(50, options.BatchSize);
        Assert.Equal(10, options.IntervalSeconds);
    }

    [Fact]
    public async Task 출고담당자한명은_피킹과포장작업을같은출고예정에연결한다()
    {
        await using var db = CreateContext();
        var outboundId = await SeedAsync(db, ("worker-a", "출고"));
        var service = CreateService(db);

        var result = await service.대기출고처리Async();

        Assert.Equal(1, result.확인출고수);
        Assert.Equal(1, result.작업생성출고수);
        Assert.Equal(2, result.생성작업수);
        Assert.Equal(0, result.작업자배정대기수);
        var tasks = await db.피킹포장작업.OrderBy(task => task.작업유형).ToArrayAsync();
        Assert.Equal(2, tasks.Length);
        Assert.All(tasks, task => Assert.Equal(outboundId, task.출고예정Id));
        Assert.All(tasks, task => Assert.Equal("worker-a", task.작업자UserId));
        Assert.Contains(tasks, task => task.작업유형 == 피킹포장작업유형.피킹);
        Assert.Contains(tasks, task => task.작업유형 == 피킹포장작업유형.포장);
    }

    [Fact]
    public async Task 같은출고를다시처리해도_작업을중복생성하지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, ("worker-a", "출고"));
        var service = CreateService(db);

        await service.대기출고처리Async();
        var replay = await service.대기출고처리Async();

        Assert.Equal(0, replay.확인출고수);
        Assert.Equal(2, await db.피킹포장작업.CountAsync());
    }

    [Fact]
    public async Task 출고작업자가없으면_가짜작업을만들지않고다음처리를기다린다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, ("stock-worker", "재고"));
        var service = CreateService(db);

        var result = await service.대기출고처리Async();

        Assert.Equal(1, result.확인출고수);
        Assert.Equal(0, result.작업생성출고수);
        Assert.Equal(1, result.작업자배정대기수);
        Assert.Empty(await db.피킹포장작업.ToArrayAsync());
    }

    [Fact]
    public async Task 피킹과포장담당자가나뉘면_두사람에게순서대로배정한다()
    {
        await using var db = CreateContext();
        var outboundId = await SeedAsync(db, ("picker-a", "피킹"), ("packer-a", "포장"));
        var service = CreateService(db);

        var result = await service.대기출고처리Async();

        Assert.Equal(1, result.작업생성출고수);
        var picking = await db.피킹포장작업.SingleAsync(task => task.작업유형 == 피킹포장작업유형.피킹);
        var packing = await db.피킹포장작업.SingleAsync(task => task.작업유형 == 피킹포장작업유형.포장);
        Assert.Equal(outboundId, picking.출고예정Id);
        Assert.Equal(outboundId, packing.출고예정Id);
        Assert.Equal("picker-a", picking.작업자UserId);
        Assert.Equal("packer-a", packing.작업자UserId);
        Assert.Equal(packing.작업Key, picking.다음작업Key);
        Assert.Equal(picking.작업Key, packing.이전작업Key);
    }

    [Fact]
    public async Task 관계형DB에서도_대기출고조회와작업투영이실행된다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(
            new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,
            new PassThroughEncryption());
        await db.Database.EnsureCreatedAsync();
        var outboundId = await SeedAsync(db, ("worker-a", "출고"));

        var result = await CreateService(db).대기출고처리Async();

        Assert.Equal(1, result.작업생성출고수);
        Assert.Equal(2, await db.피킹포장작업.CountAsync(task => task.출고예정Id == outboundId));
    }

    private static 출고피킹작업생성Service CreateService(SsalddelContext db)
        => new(
            db,
            new 피킹배치Engine(),
            new 피킹포장작업투영Service(db),
            NullLogger<출고피킹작업생성Service>.Instance);

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"outbound-picking-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryption());

    private static async Task<long> SeedAsync(
        SsalddelContext db,
        params (string UserId, string RoleName)[] users)
    {
        var now = new DateTime(2026, 9, 17, 3, 0, 0, DateTimeKind.Utc);
        var warehouse = new 창고
        {
            소유자UserId = "seller-a",
            창고명 = "사가정 출고 창고",
            주소 = "서울 중랑구 사가정로 1",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.창고.Add(warehouse);
        await db.SaveChangesAsync();

        var inbound = new 입고상품
        {
            입고요청Id = 71,
            창고Id = warehouse.Id,
            소유자UserId = "seller-a",
            판매자UserId = "seller-a",
            상품명 = "테스트 쌀",
            SKU = "RICE-1KG",
            입고수량 = 20,
            가용수량 = 10,
            예약수량 = 2,
            보관위치 = "A-01-02",
            상태 = "보관중",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.입고상품.Add(inbound);
        await db.SaveChangesAsync();

        var outbound = new 출고예정
        {
            주문참조번호 = "ORDER-001",
            입고상품Id = inbound.Id,
            판매자UserId = "seller-a",
            주문자UserId = "orderer-a",
            출고창고Id = warehouse.Id,
            상품명 = inbound.상품명,
            SKU = inbound.SKU,
            수량 = 2,
            상태 = 출고상태.예정,
            커뮤니티원장Id = "ledger-order-001",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.출고예정.Add(outbound);
        foreach (var user in users)
        {
            db.창고사용자.Add(new 창고사용자
            {
                창고Id = warehouse.Id,
                UserId = user.UserId,
                역할명 = user.RoleName,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return outbound.Id;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
