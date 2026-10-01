using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매실행ServiceTests
{
    [Fact]
    public async Task 자동집단조회_상품배송권상태를ControllerQuery로전달한다()
    {
        var client = new RecordingJsonApiClient
        {
            Response = Array.Empty<공동구매자동집단응답>()
        };
        var service = new 공동구매실행Service(client);

        await service.자동집단목록조회Async(new 공동구매자동집단조회조건
        {
            상품키 = "감자 10kg",
            배송권키 = "서울/동부",
            현재상태 = 공동구매자동집단상태코드.수요수집중
        });

        Assert.Equal(
            "api/v1/orderer/group-purchase-auto-groups"
            + $"?productKey={Uri.EscapeDataString("감자 10kg")}"
            + $"&deliveryScopeKey={Uri.EscapeDataString("서울/동부")}"
            + $"&currentStatus={공동구매자동집단상태코드.수요수집중}",
            client.LastPath);
        Assert.Equal(HttpMethod.Get, client.LastMethod);
    }

    [Fact]
    public async Task 주문원장조회_기본경로를보호형역할응답으로읽는다()
    {
        var response = new 주문원장역할별조회공개Dto { 주문원장Id = "order/root 1" };
        var client = new RecordingJsonApiClient { Response = response };
        var service = new 공동구매실행Service(client);

        var result = await service.주문원장보호조회Async("order/root 1");

        Assert.Same(response, result);
        Assert.Equal(
            $"api/v1/community/order-ledgers/{Uri.EscapeDataString("order/root 1")}",
            client.LastPath);
        Assert.Equal(typeof(주문원장역할별조회공개Dto), client.LastResponseType);
    }

    [Fact]
    public async Task 하위원장분리_Revision을ControllerQuery이름으로전달한다()
    {
        var client = new RecordingJsonApiClient
        {
            Response = new 주문원장통합공개Dto()
        };
        var service = new 공동구매실행Service(client);

        await service.하위원장분리Async("order-1", "sales-1", expectedRevision: 12);

        Assert.Equal(HttpMethod.Delete, client.LastMethod);
        Assert.Equal(
            "api/v1/community/order-ledgers/order-1/children/sales-1"
            + $"?{Uri.EscapeDataString("기대Revision")}=12",
            client.LastPath);
    }

    [Fact]
    public async Task 커머스문서조회_문서번호를Encoding하고NotFound를빈목록으로처리한다()
    {
        var client = new RecordingJsonApiClient();
        var service = new 공동구매실행Service(client);

        var result = await service.문서번호로커머스이행조회Async(" GP/2026 01 ");

        Assert.Empty(result);
        Assert.Equal(
            "api/v1/orderer/group-purchase-commerce-fulfillment-plans/lookup"
            + $"?documentManagementNumber={Uri.EscapeDataString("GP/2026 01")}",
            client.LastPath);
        Assert.True(client.LastAllowNotFound);
    }

    private sealed class RecordingJsonApiClient : IHongdalJsonApiClient
    {
        public object? Response { get; set; }
        public string? LastPath { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public object? LastRequest { get; private set; }
        public Type? LastResponseType { get; private set; }
        public bool LastAllowNotFound { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Record(HttpMethod.Get, path, null, typeof(TResponse), allowNotFound);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null, typeof(TResponse), allowNotFound);
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
            Record(method, path, request, typeof(TResponse), allowNotFound);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null, null, false);
            return Task.CompletedTask;
        }

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, request, null, false);
            return Task.CompletedTask;
        }

        private void Record(
            HttpMethod method,
            string path,
            object? request,
            Type? responseType,
            bool allowNotFound)
        {
            LastMethod = method;
            LastPath = path;
            LastRequest = request;
            LastResponseType = responseType;
            LastAllowNotFound = allowNotFound;
        }
    }
}
