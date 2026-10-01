using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 입고예정역할관점ViewModelTests
{
    [Fact]
    public void 카탈로그는_창고홈입고예정의다섯역할슬롯을정의한다()
    {
        var definitions = 입고예정역할관점카탈로그.전체;

        Assert.Equal(5, definitions.Count);
        Assert.Equal(
            BaguaTransitionCatalog.Roles.Select(role => role.RoleCode),
            definitions.Select(definition => definition.역할.RoleCode));
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BaguaBusinessCodes.Warehouse, definition.좌표.출발업무코드);
            Assert.Equal(BaguaBusinessCodes.Warehouse, definition.좌표.도착업무코드);
            Assert.Equal(입고예정역할관점카탈로그.기능코드, definition.좌표.기능코드);
            Assert.Equal(definition.역할.RoleCode, definition.좌표.역할코드);
            Assert.NotEmpty(definition.핵심정보);
            Assert.NotEmpty(definition.행동후보);
            Assert.All(definition.행동후보, action => Assert.True(action.서버권한확인필요));
        });
    }

    [Fact]
    public async Task 공통입고예정결과를_현재사용자관계와역할업무에맞게투영한다()
    {
        var items = new[]
        {
            입고(1, 주문자: "user-1", 판매자: "seller-2"),
            입고(2, 주문자: "orderer-2", 판매자: "user-1"),
            입고(
                3,
                주문자: "orderer-3",
                판매자: "seller-3",
                운송의뢰Id: "transport-3",
                원장Id: "ledger-3")
        };
        using var fixture = new Fixture(
            items,
            new 현재사용자Snapshot("user-1", "판매자 한명", [BaguaActorRoleCodes.Seller]));

        var succeeded = await fixture.창고관리자.조회Async(new 목록조회요청());
        fixture.Page.공통조회결과투영();

        Assert.True(succeeded);
        Assert.Equal(1, Assert.Single(fixture.주문자.항목목록).입고Id);
        Assert.Equal(2, Assert.Single(fixture.판매자.항목목록).입고Id);
        Assert.Equal([1L, 2L, 3L], fixture.창고관리자.항목목록.Select(item => item.입고Id));
        Assert.Equal(3, Assert.Single(fixture.운송담당자.항목목록).입고Id);
        Assert.Equal(3, Assert.Single(fixture.협동조합운영자.항목목록).입고Id);
        Assert.Equal(3, fixture.창고관리자.결과.전체건수);
        Assert.Equal(1, fixture.주문자.결과.전체건수);
        Assert.Equal(3, fixture.주문자.원본전체건수);
        Assert.Same(fixture.판매자, fixture.Page.현재관점);
    }

    [Fact]
    public async Task 다섯관점은_같은원본을서로다른핵심정보로표현한다()
    {
        var source = 입고(
            11,
            주문자: "user-1",
            판매자: "user-1",
            운송의뢰Id: "transport-11",
            원장Id: "ledger-11");
        using var fixture = new Fixture(
            [source],
            new 현재사용자Snapshot("user-1", "복수 역할 사용자", []));

        Assert.True(await fixture.창고관리자.조회Async(new 목록조회요청()));
        fixture.Page.공통조회결과투영();

        var projected = fixture.Page.역할관점목록
            .Select(viewModel => Assert.Single(viewModel.항목목록))
            .ToArray();
        Assert.All(projected, item => Assert.Same(source, item.원본));
        Assert.Equal(5, projected.Select(item => item.역할코드).Distinct().Count());
        Assert.Contains(projected[0].핵심정보, info => info.이름 == "수령·가상 창고");
        Assert.Contains(projected[1].핵심정보, info => info.이름 == "납기 약속");
        Assert.Contains(projected[2].핵심정보, info => info.이름 == "입고 흐름");
        Assert.Contains(projected[3].핵심정보, info => info.이름 == "운송 의뢰");
        Assert.Contains(projected[4].핵심정보, info => info.이름 == "공동 원장");
    }

    [Fact]
    public async Task 주문자관점은_주문자관계로제한된역할별Api를호출한다()
    {
        using var fixture = new Fixture(
            [입고(1, 주문자: "user-1", 판매자: "seller-1")],
            new 현재사용자Snapshot("user-1", "주문자", [BaguaActorRoleCodes.Orderer]));

        var succeeded = await fixture.주문자.조회Async(new 목록조회요청());

        Assert.True(succeeded);
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Equal(역할관점데이터연결상태.역할별조회연결됨, fixture.주문자.관점정의.데이터연결상태);
        Assert.Contains("warehouse-perspectives/inbounds/expected/orderer", fixture.Client.LastPath);
    }

    [Fact]
    public async Task 공동원장관점은_선택한원장Id를정규화해전용Api를호출한다()
    {
        using var fixture = new Fixture(
            [입고(1, 주문자: "user-1", 판매자: "seller-1", 원장Id: "ledger 11")],
            new 현재사용자Snapshot("user-1", "공동 원장 참여자", []));

        var succeeded = await fixture.협동조합운영자.원장별조회Async("  ledger 11  ");

        Assert.True(succeeded);
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Contains("warehouse-perspectives/inbounds/expected/community-ledgers/ledger%2011?", fixture.Client.LastPath);
    }

    [Fact]
    public async Task PageViewModel은_역할관점을선택하고지원되는관점에서만서버조회한다()
    {
        using var fixture = new Fixture(
            [입고(1, 주문자: "user-1", 판매자: "seller-1")],
            new 현재사용자Snapshot("user-1", "운송 담당", [BaguaActorRoleCodes.TransportOperator]));

        Assert.Same(fixture.운송담당자, fixture.Page.현재관점);
        Assert.True(await fixture.Page.초기화Async());
        Assert.Equal(1, fixture.Client.CallCount);
        Assert.Contains("warehouse-perspectives/inbounds/expected/transport", fixture.Client.LastPath);

        Assert.True(fixture.Page.관점선택(BaguaActorRoleCodes.WarehouseManager));
        Assert.False(fixture.Page.관점선택("unknown-role"));
        Assert.True(await fixture.Page.새로고침Async());

        Assert.Equal(2, fixture.Client.CallCount);
        Assert.Contains("status=%EC%9E%85%EA%B3%A0%EC%98%88%EC%A0%95", fixture.Client.LastPath);
        Assert.Single(fixture.창고관리자.항목목록);
    }

    [Fact]
    public void 서비스등록은_다섯역할과페이지조립ViewModel을제공한다()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        var registeredTypes = services
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType)
            .ToHashSet();
        Assert.Contains(typeof(주문자입고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(판매자입고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(창고관리자입고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(운송담당자입고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(협동조합운영자입고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(입고예정PageViewModel), registeredTypes);
        Assert.Contains(typeof(출고예정조회ViewModel), registeredTypes);
        Assert.Contains(typeof(주문자출고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(판매자출고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(창고관리자출고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(운송담당자출고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(협동조합운영자출고예정ViewModel), registeredTypes);
        Assert.Contains(typeof(출고예정PageViewModel), registeredTypes);
    }

    private static 입고요청항목응답 입고(
        long id,
        string 주문자,
        string 판매자,
        string? 운송의뢰Id = null,
        string? 원장Id = null)
        => new()
        {
            Id = id,
            창고Id = 17,
            주문Id = id + 100,
            주문참조번호 = $"ORDER-{id}",
            주문자UserId = 주문자,
            판매자UserId = 판매자,
            공급처명 = $"공급처 {id}",
            예정상품명 = $"감자 {id}",
            예정SKU = $"SKU-{id}",
            예정수량 = (int)id * 10,
            예정도착일 = new DateTime(2026, 8, Math.Clamp((int)id, 1, 28)),
            상태 = 입고상태코드.예정,
            입고흐름유형 = 입고흐름유형코드.주문자동입고예정,
            운송의뢰Id = 운송의뢰Id,
            커뮤니티원장Id = 원장Id,
            커뮤니티원장상태 = 원장Id is null ? null : "이행중"
        };

    private sealed class Fixture : IDisposable
    {
        private readonly 입고원장ViewModel _ledger;
        private readonly 입고조회ViewModel _query;

        public Fixture(
            IReadOnlyList<입고요청항목응답> items,
            현재사용자Snapshot currentUser)
        {
            Client = new RecordingJsonApiClient
            {
                Response = new 입고요청페이지응답
                {
                    Items = items,
                    TotalCount = items.Count,
                    Page = 0,
                    PageSize = 25
                }
            };
            var context = new TestCurrentUserContext(currentUser);
            var state = new 입출고화면상태ViewModel(context);
            state.창고목록적용([new() { Id = 17, 기본창고여부 = true }]);
            _ledger = new 입고원장ViewModel(new 입출고원장상태ViewModel());
            var service = new 입출고작업Service(Client);
            _query = new 입고조회ViewModel(service, state, _ledger);
            var common = new 입고예정조회ViewModel(_query);
            주문자 = new 주문자입고예정ViewModel(common, service, context);
            판매자 = new 판매자입고예정ViewModel(common, service, context);
            창고관리자 = new 창고관리자입고예정ViewModel(common, service, context);
            운송담당자 = new 운송담당자입고예정ViewModel(common, service, context);
            협동조합운영자 = new 협동조합운영자입고예정ViewModel(common, service, context);
            Page = new 입고예정PageViewModel(
                common,
                주문자,
                판매자,
                창고관리자,
                운송담당자,
                협동조합운영자,
                context);
        }

        public RecordingJsonApiClient Client { get; }
        public 주문자입고예정ViewModel 주문자 { get; }
        public 판매자입고예정ViewModel 판매자 { get; }
        public 창고관리자입고예정ViewModel 창고관리자 { get; }
        public 운송담당자입고예정ViewModel 운송담당자 { get; }
        public 협동조합운영자입고예정ViewModel 협동조합운영자 { get; }
        public 입고예정PageViewModel Page { get; }

        public void Dispose()
        {
            Page.Dispose();
            _query.Dispose();
            _ledger.Dispose();
        }
    }

    private sealed record TestCurrentUserContext(현재사용자Snapshot 현재사용자)
        : IHongdal현재사용자Context;

    private sealed class RecordingJsonApiClient : IHongdalJsonApiClient
    {
        public object? Response { get; set; }
        public int CallCount { get; private set; }
        public string? LastPath { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Record(path);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(path);
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
            Record(path);
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(path);
            return Task.CompletedTask;
        }

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(path);
            return Task.CompletedTask;
        }

        private void Record(string path)
        {
            CallCount++;
            LastPath = path;
        }
    }
}
