using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Tests.Services.Community;

public sealed class IndividualOrderPerspectiveReadServiceTests
{
    [Fact]
    public async Task 주문자는_직접참여한개별주문만조회한다()
    {
        var store = CreateStore();
        var service = CreateService(store, "orderer-1");

        var result = await service.QueryAsync(
            개별주문관점코드.주문자,
            null,
            new 개별주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        Assert.Equal(["order-1"], result.Value.Items.Select(item => item.주문원장Id));
        Assert.Equal(개별주문관점코드.주문자, result.Value.Items[0].관계코드);
        Assert.Equal("주문자 1", result.Value.Items[0].주문자표시명);
    }

    [Theory]
    [InlineData(개별주문관점코드.판매자, "seller-1", 주문원장포함역할.판매)]
    [InlineData(개별주문관점코드.창고관리자, "warehouse-1", 주문원장포함역할.창고출고)]
    [InlineData(개별주문관점코드.운송담당자, "driver-1", 주문원장포함역할.운송)]
    public async Task 업무담당자는_직접참여한하위원장역할로개별주문을조회한다(
        string perspective,
        string userId,
        string expectedRole)
    {
        var store = CreateStore();
        var service = CreateService(store, userId);

        var result = await service.QueryAsync(
            perspective,
            null,
            new 개별주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("order-1", item.주문원장Id);
        Assert.Contains(expectedRole, item.관련원장역할목록);
        Assert.Null(item.주문자표시명);
    }

    [Fact]
    public async Task 공동원장관점은_참여자에게중첩주문집계의개별주문만반환한다()
    {
        var store = CreateStore();
        var allowed = CreateService(store, "coordinator-1");
        var denied = CreateService(store, "stranger-1");

        var result = await allowed.QueryAsync(
            개별주문관점코드.공동원장,
            "  group-purchase-1  ",
            new 개별주문관점목록조회요청());
        var forbidden = await denied.QueryAsync(
            개별주문관점코드.공동원장,
            "group-purchase-1",
            new 개별주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("order-1", item.주문원장Id);
        Assert.Equal("group-purchase-1", item.공동원장Id);
        Assert.Equal("공동원장참여", item.조회근거);
        Assert.True(forbidden.IsFailed);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 검색상태정렬과페이지크기를서버에서적용한다()
    {
        var store = CreateStore();
        var service = CreateService(store, "orderer-1");

        var result = await service.QueryAsync(
            개별주문관점코드.주문자,
            null,
            new 개별주문관점목록조회요청
            {
                Search = "감자",
                Status = 커뮤니티원장상태.진행중,
                PageSize = 500,
                SortBy = nameof(개별주문관점항목응답.제목),
                SortDescending = false
            });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(100, result.Value.PageSize);
    }

    private static IndividualOrderPerspectiveReadService CreateService(FakeLedgerStore store, string userId)
        => new(
            store,
            new 주문원장역할별조회Service(store, new EmptyDisclosureStore()),
            new TestCurrentUserAccessor(userId));

    private static FakeLedgerStore CreateStore()
    {
        var order1 = Ledger("order-1", CommunityLedgerTemplateKeys.Order, "orderer-1", "주문자 1", "감자 개별 주문");
        order1.참여자목록 = [Participant("orderer-1", "주문자 1", "주문자")];
        order1.포함원장목록 =
        [
            Reference("sale-1", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, 0),
            Reference("warehouse-1", CommunityLedgerTemplateKeys.WarehouseOutbound, 주문원장포함역할.창고출고, 1),
            Reference("transport-1", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, 2)
        ];

        var order2 = Ledger("order-2", CommunityLedgerTemplateKeys.Order, "orderer-2", "주문자 2", "사과 개별 주문");
        var groupOrder = Ledger("group-order-1", CommunityLedgerTemplateKeys.GroupOrder, "coordinator-1", "공동 대표", "공동 주문집계");
        groupOrder.포함원장목록 =
        [
            Reference("order-1", CommunityLedgerTemplateKeys.Order, 주문원장포함역할.개별주문, 0)
        ];
        var groupPurchase = Ledger("group-purchase-1", CommunityLedgerTemplateKeys.GroupPurchase, "coordinator-1", "공동 대표", "감자 공동구매");
        groupPurchase.참여자목록 = [Participant("coordinator-1", "공동 대표", "공동구매 대표")];
        groupPurchase.포함원장목록 =
        [
            Reference("group-order-1", CommunityLedgerTemplateKeys.GroupOrder, 주문원장포함역할.주문집계, 0)
        ];

        return new FakeLedgerStore(
            order1,
            order2,
            Ledger("sale-1", CommunityLedgerTemplateKeys.LocalSale, "seller-1", "판매자", "감자 판매"),
            Ledger("warehouse-1", CommunityLedgerTemplateKeys.WarehouseOutbound, "warehouse-1", "창고 관리자", "감자 출고"),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport, "driver-1", "운송 기사", "감자 운송"),
            groupOrder,
            groupPurchase);
    }

    private static 커뮤니티원장Dto Ledger(
        string id,
        string template,
        string ownerId,
        string ownerName,
        string title)
        => new()
        {
            원장Id = id,
            Revision = 1,
            커뮤니티Id = "platform",
            원장템플릿Key = template,
            제목 = title,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "fulfillment",
            생성자UserId = ownerId,
            생성자표시명 = ownerName,
            생성시각Utc = new DateTime(2026, 7, 1),
            수정시각Utc = new DateTime(2026, 7, 17)
        };

    private static 커뮤니티원장참여자Dto Participant(string userId, string name, string role)
        => new() { UserId = userId, DisplayName = name, RoleLabel = role };

    private static 커뮤니티포함원장참조Dto Reference(string id, string template, string role, int order)
        => new()
        {
            원장Id = id,
            원장템플릿Key = template,
            역할 = role,
            필수여부 = true,
            표시순서 = order
        };

    private sealed class FakeLedgerStore(params 커뮤니티원장Dto[] ledgers) : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> _items =
            ledgers.ToDictionary(item => item.원장Id, StringComparer.OrdinalIgnoreCase);

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.GetValueOrDefault(원장Id.Trim()));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<커뮤니티원장Dto> result = _items.Values;
            if (!string.IsNullOrWhiteSpace(query.접근UserId))
            {
                var userId = query.접근UserId.Trim();
                result = result.Where(item =>
                    string.Equals(item.생성자UserId, userId, StringComparison.OrdinalIgnoreCase)
                    || item.참여자목록.Any(participant => string.Equals(participant.UserId, userId, StringComparison.OrdinalIgnoreCase)));
            }

            if (query.원장템플릿Keys.Count > 0)
            {
                result = result.Where(item => query.원장템플릿Keys.Contains(item.원장템플릿Key, StringComparer.OrdinalIgnoreCase));
            }

            if (query.포함원장Ids.Count > 0)
            {
                var childIds = query.포함원장Ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
                result = result.Where(item => item.포함원장목록.Any(child => childIds.Contains(child.원장Id)));
            }

            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(result.Take(query.Limit).ToArray());
        }

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

    private sealed class EmptyDisclosureStore : I주문원장공개요청저장소
    {
        public Task<IReadOnlySet<string>> 승인된대상원장Ids조회Async(
            string 주문원장Id,
            string 요청자UserId,
            IEnumerable<string> 대상원장Ids,
            DateTimeOffset 기준시각Utc,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public Task<원장공개요청기록> 요청생성Async(원장공개요청기록 요청, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<원장공개요청기록?> 요청조회Async(string 요청Id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<원장공개요청기록>> 받은요청목록Async(string 승인자UserId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<원장공개요청기록?> 요청결정Async(
            string 요청Id,
            string 승인자UserId,
            bool 승인여부,
            string? 처리메모,
            DateTimeOffset 처리시각Utc,
            DateTimeOffset 승인만료시각Utc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record TestCurrentUserAccessor(string? UserId) : ICurrentUserAccessor
    {
        public string? Role => null;
    }
}
