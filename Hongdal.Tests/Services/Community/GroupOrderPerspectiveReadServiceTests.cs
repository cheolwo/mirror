using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Tests.Services.Community;

public sealed class GroupOrderPerspectiveReadServiceTests
{
    [Fact]
    public async Task 주문자는_자신의개별주문을포함하는공동주문을조회한다()
    {
        var store = CreateStore();
        var service = CreateService(store, "orderer-1");

        var result = await service.QueryAsync(
            공동주문관점코드.주문자,
            null,
            new 공동주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("group-order-1", item.공동주문원장Id);
        Assert.Equal(1, item.개별주문수);
        Assert.Equal("감자", item.상품명);
        Assert.Equal("개별주문참여", item.조회근거);
    }

    [Theory]
    [InlineData(공동주문관점코드.판매자, "seller-1")]
    [InlineData(공동주문관점코드.창고관리자, "warehouse-1")]
    [InlineData(공동주문관점코드.운송담당자, "driver-1")]
    public async Task 업무담당자는_개별주문의하위원장참여관계로공동주문을조회한다(
        string perspective,
        string userId)
    {
        var store = CreateStore();
        var service = CreateService(store, userId);

        var result = await service.QueryAsync(
            perspective,
            null,
            new 공동주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("group-order-1", item.공동주문원장Id);
        Assert.Equal("개별주문하위원장참여", item.조회근거);
    }

    [Fact]
    public async Task 공동원장관점은_참여자에게연결된공동주문만반환한다()
    {
        var store = CreateStore();
        var allowed = CreateService(store, "coordinator-1");
        var denied = CreateService(store, "stranger-1");

        var result = await allowed.QueryAsync(
            공동주문관점코드.공동원장,
            "  group-purchase-1  ",
            new 공동주문관점목록조회요청());
        var forbidden = await denied.QueryAsync(
            공동주문관점코드.공동원장,
            "group-purchase-1",
            new 공동주문관점목록조회요청());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("group-order-1", item.공동주문원장Id);
        Assert.Equal("group-purchase-1", item.공동원장Id);
        Assert.Equal("auto-group-1", item.자동집단Id);
        Assert.True(forbidden.IsFailed);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 검색과페이지크기는_집계결과에적용된다()
    {
        var store = CreateStore();
        var service = CreateService(store, "orderer-1");

        var result = await service.QueryAsync(
            공동주문관점코드.주문자,
            null,
            new 공동주문관점목록조회요청
            {
                Search = "감자",
                Status = 커뮤니티원장상태.진행중,
                PageSize = 500,
                SortBy = nameof(공동주문관점항목응답.개별주문수)
            });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(100, result.Value.PageSize);
    }

    private static GroupOrderPerspectiveReadService CreateService(FakeLedgerStore store, string userId)
        => new(
            store,
            new 주문원장통합UseCase(store),
            new TestCurrentUserAccessor(userId));

    private static FakeLedgerStore CreateStore()
    {
        var order = Ledger("order-1", CommunityLedgerTemplateKeys.Order, "orderer-1", "감자 개별 주문");
        order.포함원장목록 =
        [
            Reference("sale-1", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, 0),
            Reference("warehouse-1", CommunityLedgerTemplateKeys.WarehouseOutbound, 주문원장포함역할.창고출고, 1),
            Reference("transport-1", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, 2)
        ];
        var groupOrder = Ledger("group-order-1", CommunityLedgerTemplateKeys.GroupOrder, "coordinator-1", "감자 공동주문 집계");
        groupOrder.외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceGroupPurchaseLedgerId"] = "group-purchase-1",
            ["AutomaticGroupId"] = "auto-group-1",
            ["ProductKey"] = "potato",
            ["ProductName"] = "감자"
        };
        groupOrder.포함원장목록 =
        [
            Reference("order-1", CommunityLedgerTemplateKeys.Order, 주문원장포함역할.개별주문, 0)
        ];
        var groupPurchase = Ledger("group-purchase-1", CommunityLedgerTemplateKeys.GroupPurchase, "coordinator-1", "감자 공동구매");
        groupPurchase.참여자목록 = [Participant("coordinator-1", "공동구매 대표")];
        groupPurchase.포함원장목록 =
        [
            Reference("group-order-1", CommunityLedgerTemplateKeys.GroupOrder, 주문원장포함역할.주문집계, 0)
        ];

        return new FakeLedgerStore(
            order,
            groupOrder,
            groupPurchase,
            Ledger("sale-1", CommunityLedgerTemplateKeys.LocalSale, "seller-1", "판매 원장"),
            Ledger("warehouse-1", CommunityLedgerTemplateKeys.WarehouseOutbound, "warehouse-1", "출고 원장"),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport, "driver-1", "운송 원장"));
    }

    private static 커뮤니티원장Dto Ledger(string id, string template, string ownerId, string title)
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
            생성자표시명 = ownerId,
            생성시각Utc = new DateTime(2026, 7, 1),
            수정시각Utc = new DateTime(2026, 7, 17)
        };

    private static 커뮤니티원장참여자Dto Participant(string userId, string role)
        => new() { UserId = userId, DisplayName = userId, RoleLabel = role };

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

            if (!string.IsNullOrWhiteSpace(query.원장템플릿Key))
            {
                result = result.Where(item => string.Equals(item.원장템플릿Key, query.원장템플릿Key, StringComparison.OrdinalIgnoreCase));
            }
            else if (query.원장템플릿Keys.Count > 0)
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

    private sealed record TestCurrentUserAccessor(string? UserId) : ICurrentUserAccessor
    {
        public string? Role => null;
    }
}
