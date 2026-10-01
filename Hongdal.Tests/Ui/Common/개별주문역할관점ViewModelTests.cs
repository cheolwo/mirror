using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 개별주문역할관점ViewModelTests
{
    [Fact]
    public void 카탈로그는_주문업무의다섯역할을정의한다()
    {
        var definitions = 개별주문역할관점카탈로그.전체;

        Assert.Equal(5, definitions.Count);
        Assert.Equal(
            BaguaTransitionCatalog.Roles.Select(role => role.RoleCode),
            definitions.Select(definition => definition.역할.RoleCode));
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BaguaBusinessCodes.Order, definition.좌표.출발업무코드);
            Assert.Equal(BaguaBusinessCodes.Order, definition.좌표.도착업무코드);
            Assert.Equal(개별주문역할관점카탈로그.기능코드, definition.좌표.기능코드);
            Assert.True(definition.서버조회준비됨);
        });
    }

    [Fact]
    public async Task 다섯관점은_각관계Api와고유표시정보를사용한다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot("user-1", "복수 역할", []));

        Assert.True(await fixture.주문자.조회Async(new 목록조회요청()));
        Assert.Contains("individual-orders/orderer?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.주문자.항목목록).핵심정보, info => info.이름 == "주문 상태");

        Assert.True(await fixture.판매자.조회Async(new 목록조회요청()));
        Assert.Contains("individual-orders/seller?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.판매자.항목목록).핵심정보, info => info.이름 == "판매 관계");

        Assert.True(await fixture.창고관리자.조회Async(new 목록조회요청()));
        Assert.Contains("individual-orders/warehouse?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.창고관리자.항목목록).핵심정보, info => info.이름 == "창고 관계");

        Assert.True(await fixture.운송담당자.조회Async(new 목록조회요청()));
        Assert.Contains("individual-orders/transport?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.운송담당자.항목목록).핵심정보, info => info.이름 == "운송 관계");

        Assert.True(await fixture.협동조합운영자.원장별조회Async("  group ledger 1  "));
        Assert.Contains("individual-orders/community-ledgers/group%20ledger%201?", fixture.Client.LastPath);
        Assert.Contains(Assert.Single(fixture.협동조합운영자.항목목록).핵심정보, info => info.이름 == "공동 원장");
    }

    [Fact]
    public async Task 페이지는_현재사용자역할을선택하고공동원장은자동조회하지않는다()
    {
        using var fixture = new Fixture(new 현재사용자Snapshot(
            "warehouse-1",
            "창고 관리자",
            [BaguaActorRoleCodes.WarehouseManager]));

        Assert.Same(fixture.창고관리자, fixture.Page.현재관점);
        Assert.True(await fixture.Page.초기화Async());
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Contains("individual-orders/warehouse?", fixture.Client.LastPath);
        var selected = Assert.Single(fixture.창고관리자.항목목록);
        Assert.True(fixture.Page.주문선택(selected));
        Assert.Equal("order-1", fixture.OrderState.선택된주문원장Id);

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
        Assert.Contains(typeof(개별주문관점Service), types);
        Assert.Contains(typeof(주문자개별주문ViewModel), types);
        Assert.Contains(typeof(판매자개별주문ViewModel), types);
        Assert.Contains(typeof(창고관리자개별주문ViewModel), types);
        Assert.Contains(typeof(운송담당자개별주문ViewModel), types);
        Assert.Contains(typeof(협동조합운영자개별주문ViewModel), types);
        Assert.Contains(typeof(개별주문PageViewModel), types);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(현재사용자Snapshot user)
        {
            Client = new RecordingJsonApiClient();
            var service = new 개별주문관점Service(Client);
            var context = new TestCurrentUserContext(user);
            주문자 = new 주문자개별주문ViewModel(service, context);
            판매자 = new 판매자개별주문ViewModel(service, context);
            창고관리자 = new 창고관리자개별주문ViewModel(service, context);
            운송담당자 = new 운송담당자개별주문ViewModel(service, context);
            협동조합운영자 = new 협동조합운영자개별주문ViewModel(service, context);
            OrderState = new 주문업무상태ViewModel(context);
            Page = new 개별주문PageViewModel(
                주문자,
                판매자,
                창고관리자,
                운송담당자,
                협동조합운영자,
                OrderState,
                context);
        }

        public RecordingJsonApiClient Client { get; }
        public 주문자개별주문ViewModel 주문자 { get; }
        public 판매자개별주문ViewModel 판매자 { get; }
        public 창고관리자개별주문ViewModel 창고관리자 { get; }
        public 운송담당자개별주문ViewModel 운송담당자 { get; }
        public 협동조합운영자개별주문ViewModel 협동조합운영자 { get; }
        public 주문업무상태ViewModel OrderState { get; }
        public 개별주문PageViewModel Page { get; }

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
            object response = new 개별주문관점페이지응답
            {
                Items =
                [
                    new 개별주문관점항목응답
                    {
                        주문원장Id = "order-1",
                        Revision = 2,
                        원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                        제목 = "감자 개별 주문",
                        상태 = "진행중",
                        현재단계Key = "fulfillment",
                        주문자표시명 = "주문자 1",
                        관계코드 = Perspective(path),
                        조회근거 = "직접참여",
                        공동원장Id = path.Contains("community-ledgers", StringComparison.Ordinal) ? "group ledger 1" : null,
                        관련원장역할목록 = [주문원장포함역할.판매, 주문원장포함역할.창고출고, 주문원장포함역할.운송],
                        관련하위원장수 = 3,
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
            => path.Contains("/seller?", StringComparison.Ordinal) ? 개별주문관점코드.판매자
                : path.Contains("/warehouse?", StringComparison.Ordinal) ? 개별주문관점코드.창고관리자
                : path.Contains("/transport?", StringComparison.Ordinal) ? 개별주문관점코드.운송담당자
                : path.Contains("/community-ledgers/", StringComparison.Ordinal) ? 개별주문관점코드.공동원장
                : 개별주문관점코드.주문자;
    }
}
