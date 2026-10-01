using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동주문역할관점ViewModelTests
{
    [Fact]
    public void 카탈로그는_주문업무의다섯역할을정의한다()
    {
        var definitions = 공동주문역할관점카탈로그.전체;

        Assert.Equal(5, definitions.Count);
        Assert.Equal(
            BaguaTransitionCatalog.Roles.Select(role => role.RoleCode),
            definitions.Select(definition => definition.역할.RoleCode));
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BaguaBusinessCodes.Order, definition.좌표.출발업무코드);
            Assert.Equal(BaguaBusinessCodes.Order, definition.좌표.도착업무코드);
            Assert.Equal(공동주문역할관점카탈로그.기능코드, definition.좌표.기능코드);
            Assert.True(definition.서버조회준비됨);
        });
    }

    [Fact]
    public async Task 다섯관점은_각관계Api와고유표시정보를사용한다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot("user-1", "복수 역할", []));

        Assert.True(await fixture.주문자.조회Async(new 목록조회요청()));
        Assert.Contains("group-orders/orderer?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.주문자.항목목록).핵심정보, info => info.이름 == "상품");

        Assert.True(await fixture.판매자.조회Async(new 목록조회요청()));
        Assert.Contains("group-orders/seller?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.판매자.항목목록).핵심정보, info => info.이름 == "판매 관계");

        Assert.True(await fixture.창고관리자.조회Async(new 목록조회요청()));
        Assert.Contains("group-orders/warehouse?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.창고관리자.항목목록).핵심정보, info => info.이름 == "창고 관계");

        Assert.True(await fixture.운송담당자.조회Async(new 목록조회요청()));
        Assert.Contains("group-orders/transport?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.운송담당자.항목목록).핵심정보, info => info.이름 == "운송 관계");

        Assert.True(await fixture.협동조합운영자.원장별조회Async("  group ledger 1  "));
        Assert.Contains("group-orders/community-ledgers/group%20ledger%201?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.협동조합운영자.항목목록).핵심정보, info => info.이름 == "공동 원장");
    }

    [Fact]
    public async Task 페이지는_현재사용자역할을선택하고실행상태의주문집계와연결한다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot(
            "warehouse-1",
            "창고 관리자",
            [BaguaActorRoleCodes.WarehouseManager]));

        Assert.Same(fixture.창고관리자, fixture.Page.현재관점);
        Assert.True(await fixture.Page.초기화Async());
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Contains("group-orders/warehouse?", fixture.Client.LastPath);

        var selected = Assert.Single(fixture.창고관리자.항목목록);
        Assert.True(fixture.Page.공동주문선택(selected));
        Assert.Equal("group-order-1", fixture.ExecutionState.공동구매주문집계원장Id);

        Assert.True(fixture.Page.관점선택(BaguaActorRoleCodes.CooperativeCoordinator));
        Assert.True(await fixture.Page.새로고침Async());
        Assert.Equal(1, fixture.Client.CallCount);
    }

    [Fact]
    public void 서비스등록은_다섯관점과페이지를제공한다()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        var types = services
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType)
            .ToHashSet();
        Assert.Contains(typeof(공동주문관점Service), types);
        Assert.Contains(typeof(주문자공동주문ViewModel), types);
        Assert.Contains(typeof(판매자공동주문ViewModel), types);
        Assert.Contains(typeof(창고관리자공동주문ViewModel), types);
        Assert.Contains(typeof(운송담당자공동주문ViewModel), types);
        Assert.Contains(typeof(협동조합운영자공동주문ViewModel), types);
        Assert.Contains(typeof(공동주문PageViewModel), types);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly 공동구매화면상태ViewModel _screenState;

        public Fixture(현재사용자Snapshot user)
        {
            Client = new RecordingJsonApiClient();
            var service = new 공동주문관점Service(Client);
            var context = new TestCurrentUserContext(user);
            주문자 = new 주문자공동주문ViewModel(service, context);
            판매자 = new 판매자공동주문ViewModel(service, context);
            창고관리자 = new 창고관리자공동주문ViewModel(service, context);
            운송담당자 = new 운송담당자공동주문ViewModel(service, context);
            협동조합운영자 = new 협동조합운영자공동주문ViewModel(service, context);
            _screenState = new 공동구매화면상태ViewModel(new FakeLedgerProgressClient(), context);
            ExecutionState = new 공동구매실행상태ViewModel(_screenState);
            Page = new 공동주문PageViewModel(
                주문자,
                판매자,
                창고관리자,
                운송담당자,
                협동조합운영자,
                ExecutionState,
                context);
        }

        public RecordingJsonApiClient Client { get; }
        public 주문자공동주문ViewModel 주문자 { get; }
        public 판매자공동주문ViewModel 판매자 { get; }
        public 창고관리자공동주문ViewModel 창고관리자 { get; }
        public 운송담당자공동주문ViewModel 운송담당자 { get; }
        public 협동조합운영자공동주문ViewModel 협동조합운영자 { get; }
        public 공동구매실행상태ViewModel ExecutionState { get; }
        public 공동주문PageViewModel Page { get; }

        public void Dispose()
        {
            Page.Dispose();
            ExecutionState.Dispose();
            _screenState.Dispose();
        }
    }

    private sealed record TestCurrentUserContext(현재사용자Snapshot 현재사용자)
        : IHongdal현재사용자Context;

    private sealed class FakeLedgerProgressClient : I공동구매원장절차Client
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
            object response = new 공동주문관점페이지응답
            {
                Items =
                [
                    new 공동주문관점항목응답
                    {
                        공동주문원장Id = "group-order-1",
                        Revision = 2,
                        제목 = "감자 공동주문 집계",
                        상태 = "진행중",
                        현재단계Key = "fulfillment",
                        관계코드 = Perspective(path),
                        조회근거 = "개별주문하위원장참여",
                        공동원장Id = path.Contains("community-ledgers", StringComparison.Ordinal) ? "group ledger 1" : null,
                        자동집단Id = "auto-group-1",
                        상품키 = "potato",
                        상품명 = "감자",
                        개별주문수 = 5,
                        완료개별주문수 = 2,
                        서명대상주문수 = 5,
                        서명완료주문수 = 3,
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
            => path.Contains("/seller?", StringComparison.Ordinal) ? 공동주문관점코드.판매자
                : path.Contains("/warehouse?", StringComparison.Ordinal) ? 공동주문관점코드.창고관리자
                : path.Contains("/transport?", StringComparison.Ordinal) ? 공동주문관점코드.운송담당자
                : path.Contains("/community-ledgers/", StringComparison.Ordinal) ? 공동주문관점코드.공동원장
                : 공동주문관점코드.주문자;
    }
}
