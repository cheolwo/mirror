using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 하차역할관점ViewModelTests
{
    [Fact]
    public void 카탈로그는_운송에서창고로이어지는다섯역할을정의한다()
    {
        var definitions = 하차역할관점카탈로그.전체;

        Assert.Equal(5, definitions.Count);
        Assert.Equal(
            BaguaTransitionCatalog.Roles.Select(role => role.RoleCode),
            definitions.Select(definition => definition.역할.RoleCode));
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BaguaBusinessCodes.Transport, definition.좌표.출발업무코드);
            Assert.Equal(BaguaBusinessCodes.Warehouse, definition.좌표.도착업무코드);
            Assert.Equal(하차역할관점카탈로그.기능코드, definition.좌표.기능코드);
            Assert.True(definition.서버조회준비됨);
        });
    }

    [Fact]
    public async Task 다섯관점은_각관계Api와고유표시정보를사용한다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot("user-1", "복수 역할", []));

        Assert.True(await fixture.주문자.조회Async(new 목록조회요청()));
        Assert.Contains("unloading-perspectives/orderer?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.주문자.항목목록).핵심정보, info => info.이름 == "내 주문");

        Assert.True(await fixture.판매자.조회Async(new 목록조회요청()));
        Assert.Contains("unloading-perspectives/seller?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.판매자.항목목록).핵심정보, info => info.이름 == "판매 주문");

        Assert.True(await fixture.창고관리자.조회Async(new 목록조회요청()));
        Assert.Contains("unloading-perspectives/warehouse?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.창고관리자.항목목록).핵심정보, info => info.이름 == "도착 창고");

        Assert.True(await fixture.운송담당자.조회Async(new 목록조회요청()));
        Assert.Contains("unloading-perspectives/transport?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.운송담당자.항목목록).핵심정보, info => info.이름 == "하차지");

        Assert.True(await fixture.협동조합운영자.원장별조회Async("  group ledger 1  "));
        Assert.Contains("unloading-perspectives/community-ledgers/group%20ledger%201?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.협동조합운영자.항목목록).핵심정보, info => info.이름 == "공동 원장");
    }

    [Fact]
    public async Task 페이지는_현재사용자역할과하차선택상태를연결한다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot(
            "driver-1",
            "운송 담당자",
            [BaguaActorRoleCodes.TransportOperator]));

        Assert.Same(fixture.운송담당자, fixture.Page.현재관점);
        Assert.True(await fixture.Page.초기화Async());
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Contains("unloading-perspectives/transport?", fixture.Client.LastPath);

        var selected = Assert.Single(fixture.운송담당자.항목목록);
        Assert.True(fixture.Page.하차선택(selected));
        Assert.Equal("201:21", fixture.State.선택된하차작업Id);
        Assert.Equal(21, fixture.State.선택된출고예정Id);
        Assert.Equal(201, fixture.State.선택된운송원장Id);
        Assert.Equal("transport-1", fixture.State.선택된운송의뢰Id);
        Assert.Equal(301, fixture.State.선택된입고요청Id);

        Assert.True(fixture.Page.관점선택(BaguaActorRoleCodes.CooperativeCoordinator));
        Assert.True(await fixture.Page.새로고침Async());
        Assert.Equal(1, fixture.Client.CallCount);
    }

    [Fact]
    public void 서비스등록은_다섯관점상태와페이지를제공한다()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        var types = services
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType)
            .ToHashSet();
        Assert.Contains(typeof(하차관점Service), types);
        Assert.Contains(typeof(하차업무상태ViewModel), types);
        Assert.Contains(typeof(주문자하차ViewModel), types);
        Assert.Contains(typeof(판매자하차ViewModel), types);
        Assert.Contains(typeof(창고관리자하차ViewModel), types);
        Assert.Contains(typeof(운송담당자하차ViewModel), types);
        Assert.Contains(typeof(협동조합운영자하차ViewModel), types);
        Assert.Contains(typeof(하차PageViewModel), types);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(현재사용자Snapshot user)
        {
            Client = new RecordingJsonApiClient();
            var service = new 하차관점Service(Client);
            var context = new TestCurrentUserContext(user);
            주문자 = new 주문자하차ViewModel(service, context);
            판매자 = new 판매자하차ViewModel(service, context);
            창고관리자 = new 창고관리자하차ViewModel(service, context);
            운송담당자 = new 운송담당자하차ViewModel(service, context);
            협동조합운영자 = new 협동조합운영자하차ViewModel(service, context);
            State = new 하차업무상태ViewModel();
            Page = new 하차PageViewModel(
                주문자,
                판매자,
                창고관리자,
                운송담당자,
                협동조합운영자,
                State,
                context);
        }

        public RecordingJsonApiClient Client { get; }
        public 주문자하차ViewModel 주문자 { get; }
        public 판매자하차ViewModel 판매자 { get; }
        public 창고관리자하차ViewModel 창고관리자 { get; }
        public 운송담당자하차ViewModel 운송담당자 { get; }
        public 협동조합운영자하차ViewModel 협동조합운영자 { get; }
        public 하차업무상태ViewModel State { get; }
        public 하차PageViewModel Page { get; }

        public void Dispose() => Page.Dispose();
    }

    private sealed record TestCurrentUserContext(현재사용자Snapshot 현재사용자)
        : IHongdal현재사용자Context;

    private sealed class RecordingJsonApiClient : IHongdalJsonApiClient
    {
        public int CallCount { get; private set; }
        public string? LastPath { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPath = path;
            object response = new 하차관점페이지응답
            {
                Items =
                [
                    new 하차관점항목응답
                    {
                        하차작업Id = "201:21",
                        출고예정Id = 21,
                        운송원장Id = 201,
                        운송의뢰Id = "transport-1",
                        운송번호 = "TR-201",
                        관계코드 = Perspective(path),
                        조회근거 = "실제관계",
                        하차상태 = 하차작업상태코드.도착,
                        운송상태 = "하차지도착",
                        하차가능여부 = true,
                        주문참조번호 = "ORDER-21",
                        주문자UserId = "orderer-1",
                        판매자UserId = "seller-1",
                        화주UserId = "shipper-1",
                        확정기사UserId = "driver-1",
                        출고창고Id = 1,
                        출고창고명 = "공동 출고 창고",
                        입고요청Id = 301,
                        도착창고Id = 2,
                        도착창고명 = "공동 도착 창고",
                        창고입고연결여부 = true,
                        상차주소 = "서울 중구 세종대로",
                        상차상세주소 = "1층",
                        하차주소 = "부산 중구 중앙대로",
                        하차상세주소 = "공동 입고장",
                        상품명 = "감자",
                        SKU = "POTATO",
                        수량 = 10,
                        공동원장Id = path.Contains("community-ledgers", StringComparison.Ordinal) ? "group ledger 1" : null,
                        수정시각Utc = new DateTime(2026, 7, 17)
                    }
                ],
                TotalCount = 1,
                Page = 0,
                PageSize = 25
            };
            return Task.FromResult((TResponse?)response);
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static string Perspective(string path)
            => path.Contains("/seller?", StringComparison.Ordinal) ? 하차업무관점코드.판매자
                : path.Contains("/warehouse?", StringComparison.Ordinal) ? 하차업무관점코드.창고관리자
                : path.Contains("/transport?", StringComparison.Ordinal) ? 하차업무관점코드.운송담당자
                : path.Contains("/community-ledgers/", StringComparison.Ordinal) ? 하차업무관점코드.공동원장
                : 하차업무관점코드.주문자;
    }
}
