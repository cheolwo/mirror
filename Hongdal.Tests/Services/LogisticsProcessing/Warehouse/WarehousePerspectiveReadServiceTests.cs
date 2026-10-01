using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Community;
using Hongdal.Services.LogisticsProcessing.Warehouse;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.도메인.운송;
using 홍달.도메인.창고;

namespace Hongdal.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class WarehousePerspectiveReadServiceTests
{
    [Fact]
    public async Task 입고예정은_주문자와판매자관계별로서로다르게필터링한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "user-1");

        var orderer = await service.QueryExpectedInboundsAsync(
            창고업무관점코드.주문자,
            null,
            new 입고요청목록조회요청(),
            default);
        var seller = await service.QueryExpectedInboundsAsync(
            창고업무관점코드.판매자,
            null,
            new 입고요청목록조회요청(),
            default);

        Assert.True(orderer.IsSuccess);
        Assert.Equal([1L], orderer.Value.Items.Select(item => item.Id));
        Assert.True(seller.IsSuccess);
        Assert.Equal([2L], seller.Value.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task 운송담당자는_추천또는확정기사로연결된입출고예정만조회한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "driver-1");

        var inbounds = await service.QueryExpectedInboundsAsync(
            창고업무관점코드.운송담당자,
            null,
            new 입고요청목록조회요청(),
            default);
        var outbounds = await service.QueryExpectedOutboundsAsync(
            창고업무관점코드.운송담당자,
            null,
            new 출고예정목록조회요청(),
            default);

        Assert.True(inbounds.IsSuccess);
        Assert.Equal([3L], inbounds.Value.Items.Select(item => item.Id));
        Assert.True(outbounds.IsSuccess);
        Assert.Equal([13L], outbounds.Value.Items.Select(item => item.Id));
        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0), outbounds.Value.Items[0].예정출고일);
    }

    [Fact]
    public async Task 공동원장조회는_생성자나참여자만해당원장범위를조회한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var ledgers = new FakeLedgerStore(
            new 커뮤니티원장Dto
            {
                원장Id = "ledger-1",
                생성자UserId = "owner-1",
                참여자목록 = [new 커뮤니티원장참여자Dto { UserId = "user-1" }]
            });
        var allowed = new WarehousePerspectiveReadService(db, new TestCurrentUserAccessor("user-1"), ledgers);
        var denied = new WarehousePerspectiveReadService(db, new TestCurrentUserAccessor("other-user"), ledgers);

        var result = await allowed.QueryExpectedInboundsAsync(
            창고업무관점코드.공동원장,
            "  ledger-1  ",
            new 입고요청목록조회요청(),
            default);
        var forbidden = await denied.QueryExpectedInboundsAsync(
            창고업무관점코드.공동원장,
            "ledger-1",
            new 입고요청목록조회요청(),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal([4L], result.Value.Items.Select(item => item.Id));
        Assert.True(forbidden.IsFailed);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 창고출고예정은_담당창고만조회하고창고와운송일정을투영한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "warehouse-user");

        var result = await service.QueryExpectedOutboundsAsync(
            창고업무관점코드.창고관리자,
            null,
            new 출고예정목록조회요청(),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal([14L, 13L, 12L, 11L], result.Value.Items.Select(item => item.Id));
        var transported = Assert.Single(result.Value.Items, item => item.Id == 13);
        Assert.Equal("공동 창고", transported.출고창고명);
        Assert.Equal("서울시 중구", transported.출고창고주소);
        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0), transported.예정출고일);
        Assert.DoesNotContain(result.Value.Items, item => item.Id == 15 || item.Id == 16);
    }

    private static WarehousePerspectiveReadService CreateService(HongdalContext db, string userId)
        => new(
            db,
            new TestCurrentUserAccessor(userId),
            new FakeLedgerStore());

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"warehouse-perspective-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private static async Task SeedAsync(HongdalContext db)
    {
        db.창고.AddRange(
            new 창고
            {
                Id = 1,
                소유자UserId = "warehouse-user",
                창고명 = "공동 창고",
                주소 = "서울시 중구"
            },
            new 창고
            {
                Id = 2,
                소유자UserId = "other-warehouse",
                창고명 = "다른 창고",
                주소 = "부산시"
            });
        db.운송원장.Add(new 운송원장
        {
            Id = 1,
            의뢰Id = "transport-1",
            원본의뢰Id = "transport-1",
            화주Id = "shipper-1",
            확정기사Id = "driver-1",
            출발_픽업 = new DateTime(2026, 8, 1, 9, 0, 0),
            도착 = new DateTime(2026, 8, 1, 15, 0, 0)
        });
        db.입고요청.AddRange(
            Inbound(1, "user-1", "seller-2"),
            Inbound(2, "orderer-2", "user-1"),
            Inbound(3, "orderer-3", "seller-3", transportId: "transport-1"),
            Inbound(4, "orderer-4", "seller-4", ledgerId: "ledger-1"),
            Inbound(5, "orderer-5", "seller-5"),
            Inbound(6, "user-1", "seller-6", status: 입고상태코드.완료));
        db.출고예정.AddRange(
            Outbound(11, "user-1", "seller-2"),
            Outbound(12, "orderer-2", "user-1"),
            Outbound(13, "orderer-3", "seller-3", transportId: "transport-1"),
            Outbound(14, "orderer-4", "seller-4", ledgerId: "ledger-1"),
            Outbound(15, "orderer-5", "seller-5", warehouseId: 2),
            Outbound(16, "warehouse-user", "seller-6", status: 출고상태코드.완료));
        await db.SaveChangesAsync();
    }

    private static 입고요청 Inbound(
        long id,
        string orderer,
        string seller,
        string? transportId = null,
        string? ledgerId = null,
        string status = 입고상태코드.예정)
        => new()
        {
            Id = id,
            창고Id = 1,
            주문자UserId = orderer,
            판매자UserId = seller,
            주문참조번호 = $"ORDER-{id}",
            공급처명 = $"SUPPLIER-{id}",
            상태 = status,
            운송의뢰Id = transportId,
            커뮤니티원장Id = ledgerId,
            CreatedAt = new DateTime(2026, 7, (int)id)
        };

    private static 출고예정 Outbound(
        long id,
        string orderer,
        string seller,
        string? transportId = null,
        string? ledgerId = null,
        long warehouseId = 1,
        string status = 출고상태코드.예정)
        => new()
        {
            Id = id,
            주문자UserId = orderer,
            판매자UserId = seller,
            주문참조번호 = $"ORDER-{id}",
            출고창고Id = warehouseId,
            상품명 = $"PRODUCT-{id}",
            SKU = $"SKU-{id}",
            수량 = 10,
            상태 = status,
            운송의뢰Id = transportId,
            커뮤니티원장Id = ledgerId,
            CreatedAt = new DateTime(2026, 7, (int)id)
        };

    private sealed record TestCurrentUserAccessor(string? UserId) : ICurrentUserAccessor
    {
        public string? Role => null;
    }

    private sealed class FakeLedgerStore(params 커뮤니티원장Dto[] ledgers) : I커뮤니티원장저장소
    {
        private readonly IReadOnlyDictionary<string, 커뮤니티원장Dto> _items
            = ledgers.ToDictionary(item => item.원장Id, StringComparer.OrdinalIgnoreCase);

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.GetValueOrDefault(원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_items.Values.ToArray());

        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
