using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매창고ServiceTests
{
    [Fact]
    public async Task 입고목록조회_Controller경로의응답항목을반환한다()
    {
        var item = new 입고요청항목응답 { Id = 17 };
        var client = new RecordingJsonApiClient
        {
            Response = new 입고요청목록응답 { Items = [item] }
        };
        var service = new 공동구매창고Service(client);

        var result = await service.입고목록조회Async();

        Assert.Same(item, Assert.Single(result));
        Assert.Equal(HttpMethod.Get, client.LastMethod);
        Assert.Equal("api/v1/warehouse-operations/inbounds", client.LastPath);
    }

    [Fact]
    public async Task 입고서버목록조회는_검색정렬페이지조건을Query경로에전달한다()
    {
        var client = new RecordingJsonApiClient
        {
            Response = new 입고요청페이지응답 { TotalCount = 31, Page = 2, PageSize = 25 }
        };
        var service = new 입출고작업Service(client);

        var result = await service.입고목록조회Async(new 입고요청목록조회요청
        {
            Page = 2,
            PageSize = 25,
            Search = "공급처 A",
            SortBy = nameof(입고요청항목응답.예정도착일),
            SortDescending = false,
            WarehouseId = 17,
            Status = "입고예정"
        });

        Assert.Equal(31, result.TotalCount);
        Assert.Equal(HttpMethod.Get, client.LastMethod);
        Assert.Contains("api/v1/warehouse-operations/inbounds/query?", client.LastPath);
        Assert.Contains("page=2", client.LastPath);
        Assert.Contains("pageSize=25", client.LastPath);
        Assert.Contains("search=%EA%B3%B5%EA%B8%89%EC%B2%98%20A", client.LastPath);
        Assert.Contains("sortBy=%EC%98%88%EC%A0%95%EB%8F%84%EC%B0%A9%EC%9D%BC", client.LastPath);
        Assert.Contains("sortDescending=false", client.LastPath);
        Assert.Contains("warehouseId=17", client.LastPath);
        Assert.Contains("status=%EC%9E%85%EA%B3%A0%EC%98%88%EC%A0%95", client.LastPath);
    }

    [Fact]
    public async Task 입고예정조회ViewModel은_상태조건을입고예정으로고정한다()
    {
        var client = new RecordingJsonApiClient
        {
            Response = new 입고요청페이지응답
            {
                Items = [new 입고요청항목응답 { Id = 7, 상태 = 입고상태코드.예정 }],
                TotalCount = 1
            }
        };
        var state = new 입출고화면상태ViewModel();
        state.창고목록적용([new() { Id = 17, 기본창고여부 = true }]);
        using var ledger = new 입고원장ViewModel(new 입출고원장상태ViewModel());
        using var query = new 입고조회ViewModel(new 입출고작업Service(client), state, ledger);
        var expectedQuery = new 입고예정조회ViewModel(query);

        var succeeded = await expectedQuery.조회Async(new 목록조회요청
        {
            검색어 = "SUP-01",
            필터조건 =
            [
                new 목록필터조건(
                    nameof(입고요청항목응답.상태),
                    "Equal",
                    입고상태코드.완료)
            ]
        });

        Assert.True(succeeded);
        Assert.Equal(1, expectedQuery.결과.전체건수);
        Assert.Contains("search=SUP-01", client.LastPath);
        Assert.Contains("warehouseId=17", client.LastPath);
        Assert.Contains("status=%EC%9E%85%EA%B3%A0%EC%98%88%EC%A0%95", client.LastPath);
        Assert.DoesNotContain("status=%EC%9E%85%EA%B3%A0%EC%99%84%EB%A3%8C", client.LastPath);
    }

    [Fact]
    public async Task 입고완료_입고번호와요청을Controller에전달한다()
    {
        var request = new 입고완료요청
        {
            Items = [new 입고상품저장요청 { 상품명 = "감자", 입고수량 = 10 }]
        };
        var client = new RecordingJsonApiClient
        {
            Response = new 입고상품목록응답()
        };
        var service = new 공동구매창고Service(client);

        await service.입고완료Async(23, request);

        Assert.Equal(HttpMethod.Post, client.LastMethod);
        Assert.Equal("api/v1/warehouse-operations/inbounds/23/complete", client.LastPath);
        Assert.Same(request, client.LastRequest);
    }

    [Fact]
    public async Task 입고수정과취소는_같은입고리소스의PutDelete경로를사용한다()
    {
        var request = new 입고요청저장요청 { 창고Id = 3, 공급처명 = "생산자" };
        var client = new RecordingJsonApiClient
        {
            Response = new 입고요청항목응답 { Id = 37 }
        };
        var service = new 공동구매창고Service(client);

        await service.입고요청수정Async(37, request);

        Assert.Equal(HttpMethod.Put, client.LastMethod);
        Assert.Equal("api/v1/warehouse-operations/inbounds/37", client.LastPath);
        Assert.Same(request, client.LastRequest);

        await service.입고요청취소Async(37);

        Assert.Equal(HttpMethod.Delete, client.LastMethod);
        Assert.Equal("api/v1/warehouse-operations/inbounds/37", client.LastPath);
        Assert.Null(client.LastRequest);
    }

    [Fact]
    public async Task 출고포장_입고상품번호를재고경로에전달한다()
    {
        var request = new 포장작업요청 { 포장수량 = 4 };
        var client = new RecordingJsonApiClient
        {
            Response = new 창고작업결과응답 { 입고상품Id = 31 }
        };
        var service = new 공동구매창고Service(client);

        await service.포장작업Async(31, request);

        Assert.Equal("api/v1/warehouse-operations/inventory/31/pack", client.LastPath);
        Assert.Same(request, client.LastRequest);
    }

    [Fact]
    public async Task 운송인계_재위탁운송Controller경로를사용한다()
    {
        var request = new 재고운송의뢰생성요청 { 입고상품Id = 31, 요청수량 = 4 };
        var client = new RecordingJsonApiClient
        {
            Response = new 화주운송의뢰응답 { 의뢰Id = "shipping-1" }
        };
        var service = new 공동구매창고Service(client);

        var result = await service.운송인계Async(request);

        Assert.Equal("shipping-1", result?.의뢰Id);
        Assert.Equal("api/v1/warehouse-operations/inventory/reconsignment", client.LastPath);
        Assert.Same(request, client.LastRequest);
    }

    private sealed class RecordingJsonApiClient : IHongdalJsonApiClient
    {
        public object? Response { get; set; }
        public string? LastPath { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public object? LastRequest { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Record(HttpMethod.Get, path, null);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, request);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null);
            return Task.CompletedTask;
        }

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, request);
            return Task.CompletedTask;
        }

        private void Record(HttpMethod method, string path, object? request)
        {
            LastMethod = method;
            LastPath = path;
            LastRequest = request;
        }
    }
}
