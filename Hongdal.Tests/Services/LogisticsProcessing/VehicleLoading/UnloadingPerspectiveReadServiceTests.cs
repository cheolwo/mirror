using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Services.Community;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.도메인.운송;
using 홍달.도메인.창고;

namespace Hongdal.Tests.Services.LogisticsProcessing.VehicleLoading;

public sealed class UnloadingPerspectiveReadServiceTests
{
    [Theory]
    [InlineData(하차업무관점코드.주문자, "orderer-1", "수령주문자연결")]
    [InlineData(하차업무관점코드.판매자, "seller-1", "판매배송연결")]
    [InlineData(하차업무관점코드.운송담당자, "driver-1", "운송원장담당")]
    public async Task 역할관점은_실제출고와운송관계로하차를조회한다(
        string perspective,
        string userId,
        string accessBasis)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, userId);

        var result = await service.QueryAsync(
            perspective,
            null,
            new 하차관점목록조회요청 { Search = "감자" });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("201:21", item.하차작업Id);
        Assert.Equal(하차작업상태코드.도착, item.하차상태);
        Assert.True(item.하차가능여부);
        Assert.False(item.하차완료여부);
        Assert.Equal(accessBasis, item.조회근거);
        Assert.Equal("부산 중구 중앙대로 공동 입고장", $"{item.하차주소} {item.하차상세주소}");
    }

    [Fact]
    public async Task 도착창고관리자는_연결된입고요청을통해하차를조회한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "destination-warehouse");

        var result = await service.QueryAsync(
            하차업무관점코드.창고관리자,
            null,
            new 하차관점목록조회요청 { WarehouseId = 2 });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(21, item.출고예정Id);
        Assert.Equal(301, item.입고요청Id);
        Assert.Equal(2, item.도착창고Id);
        Assert.Equal("공동 도착 창고", item.도착창고명);
        Assert.True(item.창고입고연결여부);
        Assert.Equal("도착창고입고담당", item.조회근거);
    }

    [Fact]
    public async Task 인수완료는_하차완료로정규화하고직송을표시한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "orderer-1");

        var result = await service.QueryAsync(
            하차업무관점코드.주문자,
            null,
            new 하차관점목록조회요청 { Status = 하차작업상태코드.완료 });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(22, item.출고예정Id);
        Assert.Equal("인수완료", item.운송상태);
        Assert.Equal(하차작업상태코드.완료, item.하차상태);
        Assert.True(item.하차완료여부);
        Assert.False(item.창고입고연결여부);
        Assert.Null(item.입고요청Id);
        Assert.Equal(new DateTime(2026, 7, 17, 15, 0, 0), item.하차완료일시);
    }

    [Fact]
    public async Task 공동원장관점은_입고원장연결도찾고참여자에게만반환한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "ledger-1",
            생성자UserId = "owner-1",
            참여자목록 = [new 커뮤니티원장참여자Dto { UserId = "coordinator-1" }]
        };
        var allowed = new UnloadingPerspectiveReadService(
            db,
            new TestCurrentUserAccessor("coordinator-1"),
            new FakeLedgerStore(ledger));
        var denied = new UnloadingPerspectiveReadService(
            db,
            new TestCurrentUserAccessor("stranger-1"),
            new FakeLedgerStore(ledger));

        var result = await allowed.QueryAsync(
            하차업무관점코드.공동원장,
            "  ledger-1  ",
            new 하차관점목록조회요청());
        var forbidden = await denied.QueryAsync(
            하차업무관점코드.공동원장,
            "ledger-1",
            new 하차관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(21, item.출고예정Id);
        Assert.Equal("ledger-1", item.공동원장Id);
        Assert.Equal("공동원장참여", item.조회근거);
        Assert.True(forbidden.IsFailed);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 도착창고와페이지조건은_서버목록에적용된다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "destination-warehouse");

        var result = await service.QueryAsync(
            하차업무관점코드.창고관리자,
            null,
            new 하차관점목록조회요청
            {
                WarehouseId = 2,
                PageSize = 500,
                SortBy = nameof(하차관점항목응답.출고예정Id),
                SortDescending = false
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([21L], result.Value.Items.Select(item => item.출고예정Id));
        Assert.Equal(200, result.Value.PageSize);
    }

    private static UnloadingPerspectiveReadService CreateService(HongdalContext db, string userId)
        => new(db, new TestCurrentUserAccessor(userId), new FakeLedgerStore());

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"unloading-perspective-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private static async Task SeedAsync(HongdalContext db)
    {
        db.창고.AddRange(
            new 창고
            {
                Id = 1,
                소유자UserId = "origin-warehouse",
                창고명 = "공동 출고 창고",
                주소 = "서울 중구 세종대로"
            },
            new 창고
            {
                Id = 2,
                소유자UserId = "destination-warehouse",
                창고명 = "공동 도착 창고",
                주소 = "부산 중구 중앙대로"
            });
        db.운송원장.AddRange(
            new 운송원장
            {
                Id = 201,
                의뢰Id = "transport-1",
                원본의뢰Id = "transport-1",
                운송번호 = "TR-201",
                화주Id = "shipper-1",
                확정기사Id = "driver-1",
                상태 = "하차지도착",
                픽업_도로명주소 = "서울 중구 세종대로",
                픽업_상세주소 = "1층",
                하차_도로명주소 = "부산 중구 중앙대로",
                하차_상세주소 = "공동 입고장",
                UpdatedAt = new DateTime(2026, 7, 17, 14, 0, 0)
            },
            new 운송원장
            {
                Id = 202,
                의뢰Id = "transport-2",
                원본의뢰Id = "transport-2",
                운송번호 = "TR-202",
                화주Id = "shipper-2",
                확정기사Id = "driver-2",
                상태 = "인수완료",
                하차_도로명주소 = "대전 유성구 대학로",
                하차_상세주소 = "구매자 자택",
                도착 = new DateTime(2026, 7, 17, 15, 0, 0),
                UpdatedAt = new DateTime(2026, 7, 17, 15, 0, 0)
            });
        db.입고요청.Add(new 입고요청
        {
            Id = 301,
            창고Id = 2,
            주문참조번호 = "ORDER-21",
            주문자UserId = "orderer-1",
            판매자UserId = "seller-1",
            출고예정Id = 21,
            운송의뢰Id = "transport-1",
            공급처명 = "감자 생산자",
            상태 = "입고예정",
            커뮤니티원장Id = "ledger-1",
            커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.GroupPurchase,
            CreatedAt = new DateTime(2026, 7, 17, 8, 0, 0),
            UpdatedAt = new DateTime(2026, 7, 17, 13, 0, 0)
        });
        db.출고예정.AddRange(
            Outbound(21, "orderer-1", "seller-1", "transport-1", "감자", null, 1, 301),
            Outbound(22, "orderer-1", "seller-2", "transport-2", "양파", null, 1, null),
            Outbound(23, "other-orderer", "other-seller", "transport-1", "배추", null, 1, null),
            Outbound(24, "orderer-1", "seller-1", null, "미연결 상품", null, 1, null));
        await db.SaveChangesAsync();
    }

    private static 출고예정 Outbound(
        long id,
        string orderer,
        string seller,
        string? transportId,
        string product,
        string? ledgerId,
        long warehouseId,
        long? inboundId)
        => new()
        {
            Id = id,
            주문자UserId = orderer,
            판매자UserId = seller,
            주문참조번호 = $"ORDER-{id}",
            출고창고Id = warehouseId,
            상품명 = product,
            SKU = $"SKU-{id}",
            수량 = 10,
            상태 = "출고예정",
            운송의뢰Id = transportId,
            입고요청Id = inboundId,
            커뮤니티원장Id = ledgerId,
            커뮤니티원장템플릿Key = ledgerId is null ? null : CommunityLedgerTemplateKeys.GroupPurchase,
            CreatedAt = new DateTime(2026, 7, 17, 8, 0, 0),
            UpdatedAt = new DateTime(2026, 7, 17, 12, 0, 0)
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
            => Task.FromResult(_items.GetValueOrDefault(원장Id.Trim()));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_items.Values.ToArray());

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
