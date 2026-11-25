using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 입고검수페이지ViewModelTests
{
    [Fact]
    public async Task 목록조회는_상세나Command를호출하지않는다()
    {
        var service = CreateService();
        var list = new 입고검수대상목록ViewModel(service);

        var loaded = await list.조회Async(0);

        Assert.True(loaded);
        Assert.Single(list.항목목록);
        Assert.Empty(service.DetailRequestIds);
        Assert.Null(service.LastInspectionRequest);
    }

    [Fact]
    public async Task 상세복귀용조건은_입력중이거나실패한조건대신_마지막조회성공을기억한다()
    {
        var service = CreateService();
        var list = new 입고검수대상목록ViewModel(service)
        {
            검색어 = " 감자 ",
            조회상태 = " 전체 "
        };

        Assert.True(await list.조회Async(1));

        list.검색어 = "미제출 검색어";
        list.조회상태 = 입고검수조회상태코드.대기;
        Assert.Equal(("감자", 입고검수조회상태코드.전체), list.적용조회조건);
        Assert.Equal(1, list.응답.Page);
        Assert.Equal(1, service.ListRequestCount);

        service.ListError = new InvalidOperationException("목록 서버 오류");
        Assert.False(await list.조회Async(0));
        Assert.Equal(("감자", 입고검수조회상태코드.전체), list.적용조회조건);
        Assert.Equal(1, list.응답.Page);

        service.ListError = null;
        list.검색어 = " ";
        Assert.True(await list.조회Async(0));
        Assert.Null(list.적용조회조건.Search);
        Assert.Equal(입고검수조회상태코드.대기, list.적용조회조건.InspectionStatus);
        Assert.Equal(0, list.응답.Page);
    }

    [Fact]
    public async Task 상세조회는_주소의Id를_다른항목으로대체하지않는다()
    {
        var service = CreateService();
        var detail = new 입고검수대상상세ViewModel(service);
        detail.조회대상설정(71);

        var loaded = await detail.조회Async();

        Assert.True(loaded);
        Assert.Equal([71L], service.DetailRequestIds);
        Assert.Equal(71, detail.항목!.InboundItemId);
        Assert.Null(service.LastInspectionRequest);
    }

    [Fact]
    public async Task 목록조회실패는_오류로남고_같은목록에서복구할수있다()
    {
        var service = CreateService();
        service.ListError = new InvalidOperationException("목록 서버 오류");
        var list = new 입고검수대상목록ViewModel(service);

        Assert.False(await list.조회Async(0));
        Assert.True(list.오류발생);
        Assert.Contains("목록 서버 오류", list.오류메시지);

        service.ListError = null;
        Assert.True(await list.조회Async(0));
        Assert.False(list.오류발생);
    }

    [Fact]
    public async Task 정확한Id가없으면_상세는_다른항목대신_대상없음을표시한다()
    {
        var service = CreateService();
        var detail = new 입고검수대상상세ViewModel(service);
        detail.조회대상설정(999);

        var loaded = await detail.조회Async();

        Assert.True(loaded);
        Assert.True(detail.대상없음);
        Assert.Null(detail.항목);
        Assert.Equal([999L], service.DetailRequestIds);
    }

    [Fact]
    public async Task 실행화면은_목록없이_정확한상세만조회한다()
    {
        var service = CreateService();
        using var page = CreateAction(service);

        var loaded = await page.초기화Async(71);

        Assert.True(loaded);
        Assert.Equal([71L], service.DetailRequestIds);
        Assert.Equal(0, service.ListRequestCount);
        Assert.Equal(12, page.작성.검수수량);
    }

    [Fact]
    public async Task 검수저장은_확인항목을검증하고_성공뒤같은Id상세만다시조회한다()
    {
        var service = CreateService();
        using var page = CreateAction(service);
        Assert.True(await page.초기화Async(71));
        page.작성.검수수량 = 12;
        page.작성.불량수량 = 2;
        page.작성.검수메모 = "박스 눌림 2개 분리";
        page.작성.수량대조확인 = true;
        page.작성.포장파손확인 = true;
        page.작성.품질기한확인 = true;
        page.작성.보관조건확인 = true;

        var saved = await page.검수후재조회Async();

        Assert.True(saved);
        Assert.Equal(71, service.LastInspectionId);
        Assert.Equal(2, service.LastInspectionRequest!.불량수량);
        Assert.Equal([71L, 71L], service.DetailRequestIds);
        Assert.Equal(0, service.ListRequestCount);
        Assert.False(page.상세.항목!.CanInspect);
        Assert.Equal("검수완료-불량포함", page.상세.항목.InventoryStatus);
    }

    [Fact]
    public async Task 검수저장은_확인항목이빠지면_Command를호출하지않는다()
    {
        var service = CreateService();
        using var page = CreateAction(service);
        Assert.True(await page.초기화Async(71));
        page.작성.수량대조확인 = true;
        page.작성.포장파손확인 = true;
        page.작성.품질기한확인 = true;

        var saved = await page.검수후재조회Async();

        Assert.False(saved);
        Assert.Null(service.LastInspectionRequest);
        Assert.Contains("네 가지", page.작성.오류메시지);
    }

    private static 입고검수실행ViewModel CreateAction(Fake입고검수페이지Service service)
        => new(
            new 입고검수대상상세ViewModel(service),
            new 입고검수작성ViewModel(service));

    private static Fake입고검수페이지Service CreateService()
        => new()
        {
            Detail = new 입고검수대상상세응답
            {
                InboundItemId = 71,
                InboundId = 41,
                WarehouseId = 7,
                WarehouseName = "공동 창고",
                ProductName = "감자",
                Sku = "POTATO-01",
                ReceivedQuantity = 12,
                AvailableQuantity = 12,
                InventoryStatus = "보관중",
                StorageCondition = "냉장",
                CanInspect = true
            }
        };

    private sealed class Fake입고검수페이지Service : I입고검수페이지Service
    {
        public int ListRequestCount { get; private set; }
        public List<long> DetailRequestIds { get; } = [];
        public long? LastInspectionId { get; private set; }
        public 입고검수요청? LastInspectionRequest { get; private set; }
        public 입고검수대상상세응답? Detail { get; set; }
        public Exception? ListError { get; set; }

        public Task<입고검수대상페이지응답> 목록조회Async(
            입고검수대상목록조회요청 request,
            CancellationToken cancellationToken = default)
        {
            ListRequestCount++;
            if (ListError is not null)
            {
                throw ListError;
            }

            입고검수대상목록항목응답[] items = Detail is null
                ? []
                :
                [
                    new 입고검수대상목록항목응답
                    {
                        InboundItemId = Detail.InboundItemId,
                        InboundId = Detail.InboundId,
                        WarehouseId = Detail.WarehouseId,
                        WarehouseName = Detail.WarehouseName,
                        ProductName = Detail.ProductName,
                        Sku = Detail.Sku,
                        ReceivedQuantity = Detail.ReceivedQuantity,
                        DefectiveQuantity = Detail.DefectiveQuantity,
                        InventoryStatus = Detail.InventoryStatus,
                        CanInspect = Detail.CanInspect
                    }
                ];
            if (request.InspectionStatus == 입고검수조회상태코드.대기)
            {
                items = items.Where(item => item.CanInspect).ToArray();
            }

            return Task.FromResult(new 입고검수대상페이지응답
            {
                Items = items,
                TotalCount = items.Length,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }

        public Task<입고검수대상상세응답?> 상세조회Async(
            long inboundItemId,
            CancellationToken cancellationToken = default)
        {
            DetailRequestIds.Add(inboundItemId);
            return Task.FromResult(Detail?.InboundItemId == inboundItemId ? Detail : null);
        }

        public Task<창고작업결과응답?> 검수Async(
            long inboundItemId,
            입고검수요청 request,
            CancellationToken cancellationToken = default)
        {
            LastInspectionId = inboundItemId;
            LastInspectionRequest = request;
            if (Detail?.InboundItemId == inboundItemId)
            {
                Detail = new 입고검수대상상세응답
                {
                    InboundItemId = Detail.InboundItemId,
                    InboundId = Detail.InboundId,
                    WarehouseId = Detail.WarehouseId,
                    WarehouseName = Detail.WarehouseName,
                    ProductName = Detail.ProductName,
                    Sku = Detail.Sku,
                    ReceivedQuantity = request.검수수량,
                    AvailableQuantity = request.검수수량 - request.불량수량,
                    DefectiveQuantity = request.불량수량,
                    InventoryStatus = request.불량수량 > 0 ? "검수완료-불량포함" : "검수완료",
                    StorageCondition = Detail.StorageCondition,
                    CanInspect = false,
                    InspectionMemo = request.검수메모
                };
            }

            return Task.FromResult<창고작업결과응답?>(new 창고작업결과응답
            {
                입고상품Id = inboundItemId,
                상태 = Detail?.InventoryStatus ?? string.Empty,
                가용수량 = Detail?.AvailableQuantity ?? 0,
                불량수량 = Detail?.DefectiveQuantity ?? 0
            });
        }
    }
}
