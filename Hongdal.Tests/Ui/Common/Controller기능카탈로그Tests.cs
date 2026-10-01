using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class Controller기능카탈로그Tests
{
    public static TheoryData<IReadOnlyList<Controller기능정의>> 역할별카탈로그 => new()
    {
        Controller기능카탈로그.공통,
        Controller기능카탈로그.기사,
        Controller기능카탈로그.음식배달기사,
        Controller기능카탈로그.화주,
        Controller기능카탈로그.주문자,
        Controller기능카탈로그.음식,
        Controller기능카탈로그.관리자
    };

    [Theory]
    [MemberData(nameof(역할별카탈로그))]
    public void 역할별카탈로그_Key와경로가중복되지않는다(IReadOnlyList<Controller기능정의> definitions)
    {
        Assert.Equal(
            definitions.Count,
            definitions.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            definitions.Count,
            definitions.Select(x => x.BasePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void 전체카탈로그_서버Controller기본경로수와일치한다()
    {
        IReadOnlyList<Controller기능정의>[] catalogs =
        [
            Controller기능카탈로그.공통,
            Controller기능카탈로그.기사,
            Controller기능카탈로그.음식배달기사,
            Controller기능카탈로그.화주,
            Controller기능카탈로그.주문자,
            Controller기능카탈로그.음식,
            Controller기능카탈로그.관리자
        ];
        var definitions = catalogs.SelectMany(x => x).ToArray();

        Assert.Equal(127, definitions.Length);
        Assert.Equal(127, definitions.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(127, definitions.Select(x => x.BasePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void 경로_제약식이있는템플릿값과상대경로를조립한다()
    {
        using var viewModel = new Controller기능ViewModel(
            new 호출금지JsonApiClient(),
            new Controller기능정의(
                "orderer.negotiation",
                "공동구매 협상",
                "api/v1/orderer/domestic-group-purchases/{campaignId:guid}/negotiation"));
        var campaignId = Guid.Parse("5f26891a-cafd-4aa4-9700-7190325d4bf0");

        var path = viewModel.경로(
            "status?includeHistory=true",
            new Dictionary<string, string> { ["campaignId"] = campaignId.ToString() });

        Assert.Equal(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation/status?includeHistory=true",
            path);
    }

    [Fact]
    public void 경로_필수템플릿값이없으면명확한예외를낸다()
    {
        using var viewModel = new Controller기능ViewModel(
            new 호출금지JsonApiClient(),
            new Controller기능정의(
                "driver.shift-detail",
                "기사별 근무",
                "api/v1/drivers/{driverId}/shifts"));

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.경로());

        Assert.Contains("driverId", exception.Message);
    }

    [Fact]
    public void 경로_상대액션경로의템플릿값도치환한다()
    {
        using var viewModel = new Controller기능ViewModel(
            new 호출금지JsonApiClient(),
            new Controller기능정의(
                "common.order-ledgers",
                "주문 원장",
                "api/v1/community/order-ledgers"));

        var path = viewModel.경로(
            "{주문원장Id}/views/warehouse",
            new Dictionary<string, string> { ["주문원장Id"] = "order/42" });

        Assert.Equal(
            "api/v1/community/order-ledgers/order%2F42/views/warehouse",
            path);
    }

    private sealed class 호출금지JsonApiClient : IHongdalJsonApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
