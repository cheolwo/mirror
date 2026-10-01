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

public sealed class LoadingPerspectiveReadServiceTests
{
    [Theory]
    [InlineData(상차업무관점코드.주문자, "orderer-1", "주문자연결")]
    [InlineData(상차업무관점코드.판매자, "seller-1", "판매자연결")]
    [InlineData(상차업무관점코드.창고관리자, "warehouse-1", "출고창고담당")]
    [InlineData(상차업무관점코드.운송담당자, "driver-1", "운송원장담당")]
    public async Task 역할관점은_실제출고와운송관계로상차를조회한다(
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
            new 상차관점목록조회요청 { Search = "감자" });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("101:11", item.상차작업Id);
        Assert.Equal(상차작업상태코드.도착, item.상차상태);
        Assert.True(item.상차가능여부);
        Assert.False(item.상차완료여부);
        Assert.Equal(accessBasis, item.조회근거);
        Assert.Equal("서울 중구 세종대로 1층", $"{item.상차주소} {item.상차상세주소}");
    }

    [Fact]
    public async Task 완료상태는_상차이후운송상태도상차완료로정규화한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "orderer-1");

        var result = await service.QueryAsync(
            상차업무관점코드.주문자,
            null,
            new 상차관점목록조회요청 { Status = 상차작업상태코드.완료 });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(12, item.출고예정Id);
        Assert.Equal("운송중", item.운송상태);
        Assert.Equal(상차작업상태코드.완료, item.상차상태);
        Assert.True(item.상차완료여부);
        Assert.Equal(new DateTime(2026, 7, 17, 10, 0, 0), item.상차완료일시);
    }

    [Fact]
    public async Task 공동원장관점은_참여자에게만해당상차를반환한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "ledger-1",
            생성자UserId = "owner-1",
            참여자목록 = [new 커뮤니티원장참여자Dto { UserId = "coordinator-1" }]
        };
        var allowed = new LoadingPerspectiveReadService(
            db,
            new TestCurrentUserAccessor("coordinator-1"),
            new FakeLedgerStore(ledger));
        var denied = new LoadingPerspectiveReadService(
            db,
            new TestCurrentUserAccessor("stranger-1"),
            new FakeLedgerStore(ledger));

        var result = await allowed.QueryAsync(
            상차업무관점코드.공동원장,
            "  ledger-1  ",
            new 상차관점목록조회요청());
        var forbidden = await denied.QueryAsync(
            상차업무관점코드.공동원장,
            "ledger-1",
            new 상차관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(11, item.출고예정Id);
        Assert.Equal("ledger-1", item.공동원장Id);
        Assert.Equal("공동원장참여", item.조회근거);
        Assert.True(forbidden.IsFailed);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 창고와페이지조건은_서버목록에적용된다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var service = CreateService(db, "warehouse-1");

        var result = await service.QueryAsync(
            상차업무관점코드.창고관리자,
            null,
            new 상차관점목록조회요청
            {
                WarehouseId = 1,
                PageSize = 500,
                SortBy = nameof(상차관점항목응답.출고예정Id),
                SortDescending = false
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([11L, 12L], result.Value.Items.Select(item => item.출고예정Id));
        Assert.Equal(200, result.Value.PageSize);
    }

    private static LoadingPerspectiveReadService CreateService(HongdalContext db, string userId)
        => new(db, new TestCurrentUserAccessor(userId), new FakeLedgerStore());

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"loading-perspective-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private static async Task SeedAsync(HongdalContext db)
    {
        db.창고.AddRange(
            new 창고
            {
                Id = 1,
                소유자UserId = "warehouse-1",
                창고명 = "공동 출고 창고",
                주소 = "서울 중구 세종대로"
            },
            new 창고
            {
                Id = 2,
                소유자UserId = "other-warehouse",
                창고명 = "다른 창고",
                주소 = "부산시"
            });
        db.운송원장.AddRange(
            new 운송원장
            {
                Id = 101,
                의뢰Id = "transport-1",
                원본의뢰Id = "transport-1",
                운송번호 = "TR-101",
                화주Id = "shipper-1",
                확정기사Id = "driver-1",
                상태 = "상차지도착",
                픽업_도로명주소 = "서울 중구 세종대로",
                픽업_상세주소 = "1층",
                하차_도로명주소 = "서울 강남구 테헤란로",
                하차_상세주소 = "공동 수령지",
                UpdatedAt = new DateTime(2026, 7, 17, 9, 0, 0)
            },
            new 운송원장
            {
                Id = 102,
                의뢰Id = "transport-2",
                원본의뢰Id = "transport-2",
                운송번호 = "TR-102",
                화주Id = "shipper-2",
                확정기사Id = "driver-2",
                상태 = "운송중",
                출발_픽업 = new DateTime(2026, 7, 17, 10, 0, 0),
                UpdatedAt = new DateTime(2026, 7, 17, 10, 0, 0)
            });
        db.출고예정.AddRange(
            Outbound(11, "orderer-1", "seller-1", "transport-1", "감자", "ledger-1", 1),
            Outbound(12, "orderer-1", "seller-2", "transport-2", "양파", null, 1),
            Outbound(13, "other-orderer", "other-seller", "transport-1", "배추", null, 2),
            Outbound(14, "orderer-1", "seller-1", null, "미연결 상품", null, 1));
        await db.SaveChangesAsync();
    }

    private static 출고예정 Outbound(
        long id,
        string orderer,
        string seller,
        string? transportId,
        string product,
        string? ledgerId,
        long warehouseId)
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
            커뮤니티원장Id = ledgerId,
            커뮤니티원장템플릿Key = ledgerId is null ? null : CommunityLedgerTemplateKeys.GroupPurchase,
            CreatedAt = new DateTime(2026, 7, (int)id),
            UpdatedAt = new DateTime(2026, 7, (int)id)
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
