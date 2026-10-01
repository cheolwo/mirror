using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 출고예정역할관점ViewModelTests
{
    [Fact]
    public void 카탈로그는_창고홈출고예정의다섯역할슬롯을정의한다()
    {
        Assert.Equal(5, 출고예정역할관점카탈로그.전체.Count);
        Assert.Equal(
            BaguaTransitionCatalog.Roles.Select(role => role.RoleCode),
            출고예정역할관점카탈로그.전체.Select(item => item.역할.RoleCode));
        Assert.All(출고예정역할관점카탈로그.전체, definition =>
        {
            Assert.Equal(BaguaBusinessCodes.Warehouse, definition.좌표.출발업무코드);
            Assert.Equal(BaguaBusinessCodes.Warehouse, definition.좌표.도착업무코드);
            Assert.Equal(출고예정역할관점카탈로그.기능코드, definition.좌표.기능코드);
            Assert.True(definition.서버조회준비됨);
            Assert.All(definition.행동후보, action => Assert.True(action.서버권한확인필요));
        });
    }

    [Fact]
    public async Task 다섯관점은_각관계Api와고유표시정보를사용한다()
    {
        var source = 출고(
            11,
            주문자: "user-1",
            판매자: "user-1",
            운송의뢰Id: "transport-11",
            원장Id: "ledger 11");
        using var fixture = new Fixture(
            [source],
            new 현재사용자Snapshot("user-1", "복수 역할", []));

        Assert.True(await fixture.주문자.조회Async(new 목록조회요청()));
        Assert.Contains("outbounds/expected/orderer?", fixture.Client.LastPath);
        Assert.True(await fixture.판매자.조회Async(new 목록조회요청()));
        Assert.Contains("outbounds/expected/seller?", fixture.Client.LastPath);
        Assert.True(await fixture.창고관리자.조회Async(new 목록조회요청()));
        Assert.Contains("outbounds/expected/warehouse?", fixture.Client.LastPath);
        Assert.True(await fixture.운송담당자.조회Async(new 목록조회요청()));
        Assert.Contains("outbounds/expected/transport?", fixture.Client.LastPath);
        Assert.True(await fixture.협동조합운영자.원장별조회Async("  ledger 11  "));
        Assert.Contains("outbounds/expected/community-ledgers/ledger%2011?", fixture.Client.LastPath);

        var projected = fixture.Page.역할관점목록
            .Select(viewModel => Assert.Single(viewModel.항목목록))
            .ToArray();
        Assert.All(projected, item => Assert.Same(source, item.원본));
        Assert.Contains(projected[0].핵심정보, info => info.이름 == "내 주문");
        Assert.Contains(projected[1].핵심정보, info => info.이름 == "판매 주문");
        Assert.Contains(projected[2].핵심정보, info => info.이름 == "출고 묶음");
        Assert.Contains(projected[3].핵심정보, info => info.이름 == "상차 창고");
        Assert.Contains(projected[4].핵심정보, info => info.이름 == "공동 원장");
    }

    [Fact]
    public async Task 창고관점공통결과는_다섯화면에읽기전용으로투영된다()
    {
        var source = 출고(
            21,
            주문자: "user-1",
            판매자: "user-1",
            운송의뢰Id: "transport-21",
            원장Id: "ledger-21");
        using var fixture = new Fixture(
            [source],
            new 현재사용자Snapshot("user-1", "창고 담당", [BaguaActorRoleCodes.WarehouseManager]));

        Assert.True(await fixture.Page.초기화Async());

        Assert.Same(fixture.창고관리자, fixture.Page.현재관점);
        Assert.Equal(5, fixture.Page.역할관점목록.Count(item => item.항목목록.Count == 1));
        Assert.Equal(1, fixture.Client.CallCount);
    }

    private static 출고예정항목응답 출고(
        long id,
        string 주문자,
        string 판매자,
        string? 운송의뢰Id,
        string? 원장Id)
        => new()
        {
            Id = id,
            주문Id = id + 100,
            주문참조번호 = $"ORDER-{id}",
            주문자UserId = 주문자,
            판매자UserId = 판매자,
            출고창고Id = 17,
            출고창고명 = "서울 공동창고",
            출고창고주소 = "서울시 중구",
            출고묶음Id = id + 200,
            상품명 = $"감자 {id}",
            SKU = $"SKU-{id}",
            수량 = (int)id,
            상태 = 출고상태코드.예정,
            운송의뢰Id = 운송의뢰Id,
            커뮤니티원장Id = 원장Id,
            커뮤니티원장상태 = "이행중",
            예정출고일 = new DateTime(2026, 8, 1, 9, 0, 0),
            예정도착일 = new DateTime(2026, 8, 1, 15, 0, 0),
            생성일시 = new DateTime(2026, 7, 17)
        };

    private sealed class Fixture : IDisposable
    {
        public Fixture(IReadOnlyList<출고예정항목응답> items, 현재사용자Snapshot user)
        {
            Client = new RecordingJsonApiClient
            {
                Response = new 출고예정페이지응답
                {
                    Items = items,
                    TotalCount = items.Count,
                    Page = 0,
                    PageSize = 25
                }
            };
            var context = new TestCurrentUserContext(user);
            var state = new 입출고화면상태ViewModel(context);
            state.창고목록적용([new 창고요약응답 { Id = 17, 창고명 = "서울 공동창고", 기본창고여부 = true }]);
            var service = new 입출고작업Service(Client);
            var common = new 출고예정조회ViewModel(service, state);
            주문자 = new 주문자출고예정ViewModel(common, service, context);
            판매자 = new 판매자출고예정ViewModel(common, service, context);
            창고관리자 = new 창고관리자출고예정ViewModel(common, service, context);
            운송담당자 = new 운송담당자출고예정ViewModel(common, service, context);
            협동조합운영자 = new 협동조합운영자출고예정ViewModel(common, service, context);
            Page = new 출고예정PageViewModel(
                common,
                주문자,
                판매자,
                창고관리자,
                운송담당자,
                협동조합운영자,
                context);
        }

        public RecordingJsonApiClient Client { get; }
        public 주문자출고예정ViewModel 주문자 { get; }
        public 판매자출고예정ViewModel 판매자 { get; }
        public 창고관리자출고예정ViewModel 창고관리자 { get; }
        public 운송담당자출고예정ViewModel 운송담당자 { get; }
        public 협동조합운영자출고예정ViewModel 협동조합운영자 { get; }
        public 출고예정PageViewModel Page { get; }

        public void Dispose() => Page.Dispose();
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
            CallCount++;
            LastPath = path;
            return Task.FromResult(Response is null ? default : (TResponse)Response);
        }

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
