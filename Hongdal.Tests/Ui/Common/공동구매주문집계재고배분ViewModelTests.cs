using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매주문집계재고배분ViewModelTests
{
    [Fact]
    public void 개별주문집합을_공동주문으로집계하고_서버배분을_창고출고초안에연결한다()
    {
        using var screen = new 공동구매화면상태ViewModel(new NoopLedgerProgressClient());
        using var execution = new 공동구매실행상태ViewModel(screen);
        execution.자동집단적용(Group());
        var warehouse = new 공동구매창고상태ViewModel();
        warehouse.창고목록적용(
        [
            new 창고요약응답
            {
                Id = 10,
                창고명 = "서울 3PL",
                주소 = "서울시 중구 창고로 10",
                기본창고여부 = true
            }
        ]);
        warehouse.재고목록적용(
        [
            new 재고항목응답
            {
                입고상품Id = 51,
                창고Id = 10,
                창고명 = "서울 3PL",
                SKU = "SKU-1",
                상품명 = "감자",
                가용수량 = 5
            }
        ]);
        using var allocation = new 공동구매재고배분ViewModel(
            new 공동구매주문집계ViewModel(execution),
            warehouse);

        Assert.True(allocation.주문집계.집계완료);
        Assert.Equal("aggregation-1", allocation.주문집계.공동구매주문집계원장Id);
        Assert.Equal(2, allocation.주문집계.개별주문수);
        Assert.Equal(5, Assert.Single(allocation.주문집계.수량집계).총주문수량);
        Assert.Equal(2, allocation.주문집계.입고예정주문수);
        Assert.Equal(1, allocation.주문집계.가상창고주문수);
        Assert.True(allocation.참고재고충족);

        var physical = allocation.출고배치초안목록.Single(x => x.개별주문원장Id == "order-1");
        var virtualHome = allocation.출고배치초안목록.Single(x => x.개별주문원장Id == "order-2");
        Assert.Equal("서울시 중구 창고로 10", physical.목적지주소);
        Assert.False(virtualHome.목적지확인됨);
        virtualHome.목적지주소 = "서울시 종로구 자택로 2";
        Assert.True(allocation.선호재고선택("order-1", 51));
        Assert.True(allocation.요청초안준비완료);
        Assert.Equal(51, physical.요청생성().Lines.Single().PreferredInboundProductId);

        allocation.서버계획적용("order-1", new OutboundBatchPlanResult
        {
            IsComplete = true,
            Message = "배분 완료",
            Allocations =
            [
                new OutboundBatchAllocation
                {
                    LineKey = physical.라인Key,
                    InboundProductId = 51,
                    WarehouseId = 10,
                    WarehouseName = "서울 3PL",
                    Sku = "SKU-1",
                    ProductName = "감자",
                    Quantity = 3
                }
            ]
        });

        using var outbound = new 공동구매출고원장ViewModel(new NoopWarehouseService(), warehouse);
        outbound.재고배분연결(allocation);

        Assert.True(outbound.배분출고선택("order-1", 51));
        Assert.Equal("aggregation-1", outbound.공동구매주문집계원장Id);
        Assert.Equal("order-1", outbound.선택된개별주문원장Id);
        Assert.Equal(3, outbound.포장초안.포장수량);
        Assert.Equal(3, outbound.운송인계초안.요청수량);
        Assert.Equal("서울시 중구 창고로 10", outbound.운송인계초안.하차지주소);
    }

    [Fact]
    public void 주문집계는_개별주문원장누락과중복을_출고전에차단한다()
    {
        using var screen = new 공동구매화면상태ViewModel(new NoopLedgerProgressClient());
        using var execution = new 공동구매실행상태ViewModel(screen);
        var group = Group();
        group.수요목록 =
        [
            group.수요목록[0],
            new 공동구매자동수요응답
            {
                수요Id = "demand-duplicate",
                개별주문원장Id = "order-1",
                희망수량 = 1,
                수량단위 = "개"
            },
            new 공동구매자동수요응답
            {
                수요Id = "demand-missing",
                희망수량 = 1,
                수량단위 = "개"
            }
        ];
        execution.자동집단적용(group);
        using var aggregate = new 공동구매주문집계ViewModel(execution);

        Assert.False(aggregate.집계완료);
        Assert.Contains(aggregate.검증오류, error => error.Contains("연결되지 않은", StringComparison.Ordinal));
        Assert.Contains(aggregate.검증오류, error => error.Contains("중복", StringComparison.Ordinal));
    }

    private static 공동구매자동집단응답 Group()
        => new()
        {
            자동집단Id = "group-1",
            공동구매주문집계원장Id = "aggregation-1",
            상품키 = "SKU-1",
            상품명 = "감자",
            총희망수량 = 5,
            수량단위 = "개",
            수요목록 =
            [
                new 공동구매자동수요응답
                {
                    수요Id = "demand-1",
                    개별주문원장Id = "order-1",
                    입고예정원장Id = "inbound-1",
                    주문자키 = "buyer-1",
                    주문자표시명 = "주문자 1",
                    도착창고Id = 10,
                    도착창고유형 = 창고유형코드.실제창고,
                    도착창고명 = "서울 3PL",
                    입고의미상태 = 공동구매개별주문입고상태코드.입고예정,
                    희망수량 = 3,
                    수량단위 = "개"
                },
                new 공동구매자동수요응답
                {
                    수요Id = "demand-2",
                    개별주문원장Id = "order-2",
                    입고예정원장Id = "inbound-2",
                    주문자키 = "buyer-2",
                    주문자표시명 = "주문자 2",
                    도착창고유형 = 창고유형코드.가상창고,
                    도착창고명 = "자택 가상 창고",
                    수령지주소참조키 = "virtual-warehouse:buyer-2",
                    입고의미상태 = 공동구매개별주문입고상태코드.입고예정,
                    희망수량 = 2,
                    수량단위 = "개"
                }
            ]
        };

    private sealed class NoopLedgerProgressClient : I공동구매원장절차Client
    {
        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
            Guid campaignId,
            CommunityGroupPurchaseLedgerProgressRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);
    }

    private sealed class NoopWarehouseService : I공동구매창고Service
    {
        public Task<IReadOnlyList<창고요약응답>> 창고목록조회Async(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<창고요약응답>>([]);
        public Task<창고요약응답?> 창고생성Async(창고저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고요약응답?>(null);
        public Task<창고요약응답?> 창고수정Async(long warehouseId, 창고저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고요약응답?>(null);
        public Task 창고삭제Async(long warehouseId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<창고사용자항목응답>> 창고사용자목록조회Async(long warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<창고사용자항목응답>>([]);
        public Task<창고사용자항목응답?> 창고사용자추가Async(long warehouseId, 창고사용자저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고사용자항목응답?>(null);
        public Task<창고사용자항목응답?> 창고사용자수정Async(long warehouseId, long warehouseUserId, 창고사용자저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고사용자항목응답?>(null);
        public Task 창고사용자삭제Async(long warehouseId, long warehouseUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<입고요청항목응답>> 입고목록조회Async(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<입고요청항목응답>>([]);
        public Task<입고요청페이지응답> 입고예정관점목록조회Async(string perspectiveCode, string? communityLedgerId, 입고요청목록조회요청 request, CancellationToken cancellationToken = default) => Task.FromResult(new 입고요청페이지응답());
        public Task<출고예정페이지응답> 출고예정관점목록조회Async(string perspectiveCode, string? communityLedgerId, 출고예정목록조회요청 request, CancellationToken cancellationToken = default) => Task.FromResult(new 출고예정페이지응답());
        public Task<입고요청항목응답?> 입고요청생성Async(입고요청저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<입고요청항목응답?>(null);
        public Task<입고요청항목응답?> 입고요청수정Async(long inboundId, 입고요청저장요청 request, CancellationToken cancellationToken = default) => Task.FromResult<입고요청항목응답?>(null);
        public Task 입고요청취소Async(long inboundId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<입고상품항목응답>> 입고완료Async(long inboundId, 입고완료요청 request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<입고상품항목응답>>([]);
        public Task<IReadOnlyList<재고항목응답>> 재고목록조회Async(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<재고항목응답>>([]);
        public Task<창고작업결과응답?> 입고검수Async(long inboundItemId, 입고검수요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고작업결과응답?>(null);
        public Task<창고작업결과응답?> 적재위치배정Async(long inboundItemId, 적재위치배정요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고작업결과응답?>(null);
        public Task<창고작업결과응답?> 포장작업Async(long inboundItemId, 포장작업요청 request, CancellationToken cancellationToken = default) => Task.FromResult<창고작업결과응답?>(null);
        public Task<화주운송의뢰응답?> 운송인계Async(재고운송의뢰생성요청 request, CancellationToken cancellationToken = default) => Task.FromResult<화주운송의뢰응답?>(null);
    }
}
