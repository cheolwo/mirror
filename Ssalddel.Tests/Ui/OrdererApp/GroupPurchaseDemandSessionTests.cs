using MudBlazor;
using OrdererApp.Components.GroupPurchase;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.OrdererApp;

public sealed class GroupPurchaseDemandSessionTests
{
    [Fact]
    public void 출처키는_사용자만trim한상품별SHA앞32자를유지한다()
    {
        Assert.Equal("orderer-app:b3750579b55ddddbc6b1e701c29a21a6",
            GroupPurchaseDemandSession.CreateDemandSourceKey(" user-1 ", "product-1"));
        Assert.NotEqual(GroupPurchaseDemandSession.CreateDemandSourceKey("user-1", "product-1"),
            GroupPurchaseDemandSession.CreateDemandSourceKey("user-1", "product-1 "));
        var error = Assert.Throws<InvalidOperationException>(() =>
            GroupPurchaseDemandSession.CreateDemandSourceKey(null, "product-1"));
        Assert.Equal("로그인 사용자 식별자를 찾을 수 없습니다.", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 권장후보또는첫후보를선택해도_사용자확인은다시필요하다(bool recommended)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var key = f.Session.RegisteredDemandSourceKey;
        var first = Scope("first");
        var second = Scope("second", recommended);
        await f.Session.ResolveDeliveryScopesAsync("KR", " 원문 주소 ", request =>
        {
            Assert.True(f.Session.IsDeliveryScopeLoading);
            Assert.Null(f.Session.RegisteredGroup);
            Assert.False(f.Session.DeliveryScopeConfirmed);
            Assert.Equal("KR", request.MarketCode);
            Assert.Equal(" 원문 주소 ", request.Address);
            Assert.Equal(1, request.ParticipantCount);
            return Task.FromResult<OperatingMarketDeliveryScopePlan?>(new() { Success = true, Items = [first, second] });
        });

        Assert.Equal(recommended ? "second" : "first", f.Session.GroupScopeKey);
        Assert.False(f.Session.DeliveryScopeConfirmed);
        Assert.Equal(key, f.Session.RegisteredDemandSourceKey);
        Assert.Equal(Severity.Success, f.Session.DeliveryScopeSeverity);
        Assert.Contains("사용자가 최종 확인해야 등록됩니다.", f.Session.DeliveryScopeMessage);
        Assert.False(f.Session.IsDeliveryScopeLoading);
    }

    [Fact]
    public async Task 빈주소는_기존선택과등록결과를지우지않는다()
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var group = f.Session.RegisteredGroup;
        await f.Session.ResolveDeliveryScopesAsync("KR", " ", _ => throw new Exception("must not call"));

        Assert.Same(group, f.Session.RegisteredGroup);
        Assert.Equal("scope-1", f.Session.GroupScopeKey);
        Assert.True(f.Session.DeliveryScopeConfirmed);
        Assert.Equal("모집권을 확인할 주소를 입력해 주세요.", f.Session.DeliveryScopeMessage);
        Assert.Equal(Severity.Warning, f.Session.DeliveryScopeSeverity);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("error")]
    [InlineData("throw")]
    public async Task 모집권실패의문구와예외전파및loading정리를유지한다(string result)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var key = f.Session.RegisteredDemandSourceKey;
        Task<OperatingMarketDeliveryScopePlan?> Resolve(OperatingMarketDeliveryScopeResolveRequest _)
            => result == "throw" ? Task.FromException<OperatingMarketDeliveryScopePlan?>(new InvalidOperationException("provider"))
                : Task.FromResult<OperatingMarketDeliveryScopePlan?>(result == "null" ? null
                    : new() { Success = result == "empty", ErrorMessage = result == "error" ? "원문 실패" : null });

        if (result == "throw")
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Session.ResolveDeliveryScopesAsync("KR", "주소", Resolve));
        else
        {
            await f.Session.ResolveDeliveryScopesAsync("KR", "주소", Resolve);
            Assert.Equal(result == "null" ? Severity.Error : Severity.Warning, f.Session.DeliveryScopeSeverity);
            Assert.Equal(result == "null" ? "주소 서비스에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요."
                : result == "error" ? "원문 실패" : "이 주소에서 사용할 모집권 후보를 찾지 못했습니다.", f.Session.DeliveryScopeMessage);
        }
        Assert.False(f.Session.IsDeliveryScopeLoading);
        Assert.False(f.Session.DeliveryScopeConfirmed);
        Assert.Equal(string.Empty, f.Session.GroupScopeKey);
        Assert.Null(f.Session.RegisteredGroup);
        Assert.Equal(key, f.Session.RegisteredDemandSourceKey);
    }

    [Fact]
    public async Task 등록명령과정본조회는순서대로실행하고_정본만표시한다()
    {
        var f = new Fixture();
        f.Register = request =>
        {
            Assert.True(f.Session.IsSubmitting);
            Assert.Null(f.Session.RegisteredGroup);
            Assert.Equal("source", request.수요출처키);
            Assert.Equal("product-1", request.상품키);
            return Task.FromResult<공동구매자동집단사용자응답?>(new() { 자동집단Id = "group", 수요건수 = 999 });
        };
        f.List = (product, scope) =>
        {
            Assert.Equal("source", f.Session.RegisteredDemandSourceKey);
            Assert.Equal("product-1", product);
            Assert.Equal("scope-1", scope);
            return Task.FromResult<IReadOnlyList<공동구매자동집단요약응답>?>([f.Canonical]);
        };

        await f.RegisterAsync();

        Assert.Equal(["register", "list"], f.Calls);
        Assert.Same(f.Canonical, f.Session.RegisteredGroup);
        Assert.Equal(Severity.Success, f.Session.StatusSeverity);
        Assert.Equal("수요 등록 후 서버 원장을 다시 확인했습니다. 현재 4건, 12kg이 같은 상품·모집권에 모였습니다.", f.Session.StatusMessage);
        Assert.False(f.Session.IsSubmitting);
    }

    [Theory]
    [InlineData("null-command")]
    [InlineData("null-list")]
    [InlineData("case-mismatch")]
    public async Task 등록실패와등록성공후정본미확보를구분한다(string scenario)
    {
        var f = new Fixture();
        if (scenario == "null-command") f.Register = _ => Task.FromResult<공동구매자동집단사용자응답?>(null);
        f.List = (_, _) => Task.FromResult<IReadOnlyList<공동구매자동집단요약응답>?>(scenario == "case-mismatch"
            ? [new() { 자동집단Id = "GROUP" }] : null);

        await f.RegisterAsync();

        Assert.Equal(scenario == "null-command" ? Severity.Error : Severity.Warning, f.Session.StatusSeverity);
        Assert.Equal(scenario == "null-command" ? "" : "source", f.Session.RegisteredDemandSourceKey);
        Assert.Null(f.Session.RegisteredGroup);
        Assert.Equal(scenario == "null-command" ? "공동 주문 수요를 서버에 등록하지 못했습니다. 잠시 후 다시 시도해 주세요."
            : "상품1 수요는 등록되었지만 자동집단을 다시 읽지 못했습니다. 새로고침 후 확인해 주세요.", f.Session.StatusMessage);
        Assert.Equal(scenario == "null-command" ? 1 : 2, f.Calls.Count);
        Assert.False(f.Session.IsSubmitting);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 503)]
    [InlineData(false, 0)]
    [InlineData(true, 401)]
    [InlineData(true, 503)]
    [InlineData(true, 0)]
    public async Task 등록명령과후속조회예외는_기존Error와401인증요구를유지한다(bool afterCommand, int status)
    {
        var f = new Fixture();
        if (afterCommand) f.List = (_, _) => Task.FromException<IReadOnlyList<공동구매자동집단요약응답>?>(Failure(status));
        else f.Register = _ => Task.FromException<공동구매자동집단사용자응답?>(Failure(status));

        await f.RegisterAsync();

        Assert.Equal(Severity.Error, f.Session.StatusSeverity);
        Assert.Equal(afterCommand ? "source" : "", f.Session.RegisteredDemandSourceKey);
        Assert.Equal(status == 401 ? 1 : 0, f.AuthenticationRequests);
        Assert.Equal(status == 401 ? "로그인 세션이 만료되었습니다. 다시 로그인해 주세요."
            : status == 0 ? "공동주문 API에 연결하지 못했습니다. 서버 상태를 확인해 주세요."
            : "공동주문 수요 원장을 저장하거나 다시 읽지 못했습니다. 잠시 후 다시 시도해 주세요.", f.Session.StatusMessage);
        Assert.False(f.Session.IsSubmitting);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 503)]
    [InlineData(false, 0)]
    [InlineData(true, 401)]
    [InlineData(true, 503)]
    [InlineData(true, 0)]
    public async Task 철회명령예외와철회성공후조회예외는_키와인증및ErrorWarning이다르다(bool afterCommand, int status)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var prior = f.Session.RegisteredGroup;
        if (afterCommand) f.List = (_, _) => Task.FromException<IReadOnlyList<공동구매자동집단요약응답>?>(Failure(status));
        else f.Withdraw = (_, _) => Task.FromException<공동구매자동수요철회응답?>(Failure(status));

        await f.WithdrawAsync();

        Assert.Equal(afterCommand ? Severity.Warning : Severity.Error, f.Session.StatusSeverity);
        Assert.Equal(afterCommand ? "" : "source", f.Session.RegisteredDemandSourceKey);
        Assert.Same(afterCommand ? null : prior, f.Session.RegisteredGroup);
        Assert.Equal(!afterCommand && status == 401 ? 1 : 0, f.AuthenticationRequests);
        Assert.Equal(afterCommand ? "비구속 수요 철회는 완료됐지만 최신 자동집단 원장을 다시 읽지 못했습니다. 새로고침 후 모집 현황을 확인해 주세요."
            : status == 401 ? "로그인 세션이 만료되었습니다. 다시 로그인해 주세요."
            : status == 0 ? "공동주문 API에 연결하지 못했습니다. 서버 상태를 확인해 주세요."
            : "비구속 수요를 철회하지 못했습니다. 잠시 후 다시 시도해 주세요.", f.Session.StatusMessage);
        Assert.False(f.Session.IsSubmitting);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 철회미완료는등록키와기존정본을유지한다(bool nullResponse)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var group = f.Session.RegisteredGroup;
        f.Calls.Clear();
        f.Withdraw = (_, _) => Task.FromResult<공동구매자동수요철회응답?>(nullResponse ? null : new() { 안내 = "철회 보류" });

        await f.WithdrawAsync();

        Assert.Equal(["withdraw"], f.Calls);
        Assert.Equal("source", f.Session.RegisteredDemandSourceKey);
        Assert.Same(group, f.Session.RegisteredGroup);
        Assert.Equal(Severity.Error, f.Session.StatusSeverity);
        Assert.Equal(nullResponse ? "비구속 수요를 철회하지 못했습니다. 잠시 후 다시 시도해 주세요." : "철회 보류", f.Session.StatusMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 철회성공은키를비운뒤_응답집단Id가없으면기존집단Id로정본을찾는다(bool canonicalFound)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        f.Withdraw = (_, _) => Task.FromResult<공동구매자동수요철회응답?>(new() { 철회완료 = true });
        f.List = (_, _) =>
        {
            Assert.Equal("", f.Session.RegisteredDemandSourceKey);
            Assert.Null(f.Session.RegisteredGroup);
            return Task.FromResult<IReadOnlyList<공동구매자동집단요약응답>?>(canonicalFound ? [f.Canonical] : null);
        };

        await f.WithdrawAsync();

        Assert.Equal(Severity.Success, f.Session.StatusSeverity);
        Assert.Equal("", f.Session.RegisteredDemandSourceKey);
        Assert.Same(canonicalFound ? f.Canonical : null, f.Session.RegisteredGroup);
        Assert.Equal(canonicalFound ? "비구속 수요를 철회한 뒤 서버 원장을 다시 확인했습니다. 남은 수요 4건, 12kg입니다."
            : "비구속 수요를 철회하고 서버 원장을 다시 확인했습니다. 이 상품·배송권에는 현재 공개할 자동집단이 없습니다.", f.Session.StatusMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 상품변경과로그아웃은_표시결과만지우고출처키와선택확인을유지한다(bool productChange)
    {
        var f = new Fixture();
        await f.RegisterAsync();
        var group = f.Session.RegisteredGroup;
        f.Session.ChangeProduct("product-1");
        Assert.Same(group, f.Session.RegisteredGroup);
        Assert.NotNull(f.Session.StatusMessage);
        if (productChange) f.Session.ChangeProduct("product-2");
        else f.Session.ClearResult();

        Assert.Null(f.Session.RegisteredGroup);
        Assert.Null(f.Session.StatusMessage);
        Assert.Equal("source", f.Session.RegisteredDemandSourceKey);
        Assert.Equal("scope-1", f.Session.GroupScopeKey);
        Assert.True(f.Session.DeliveryScopeConfirmed);
        Assert.Equal(Severity.Success, f.Session.StatusSeverity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 명령대기중상품과범위가바뀌어도_후속조회는그때의값을읽고_늦은결과도기존대로적용한다(bool register)
    {
        var f = new Fixture();
        if (!register) await f.RegisterAsync();
        var commandGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGate = new TaskCompletionSource<IReadOnlyList<공동구매자동집단요약응답>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Register = async _ => { await commandGate.Task; return new() { 자동집단Id = "group" }; };
        f.Withdraw = async (_, _) => { await commandGate.Task; return new() { 철회완료 = true, 자동집단Id = "group" }; };
        var queried = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.List = (product, scope) =>
        {
            Assert.Equal("product-2", product);
            Assert.Equal("scope-2", scope);
            queried.SetResult(true);
            return queryGate.Task;
        };
        var pending = register ? f.RegisterAsync() : f.WithdrawAsync();
        Assert.True(f.Session.IsSubmitting);
        f.Product = Product("product-2", "상품2");
        f.Session.ChangeProduct(f.Product.상품카드Id);
        f.Session.SelectDeliveryScope(Scope("scope-2"));
        commandGate.SetResult(true);
        await Task.WhenAny(queried.Task, pending);
        if (pending.IsCompleted) await pending;
        Assert.True(queried.Task.IsCompletedSuccessfully);
        f.Product = Product("product-3", "상품3");
        f.Session.ChangeProduct(f.Product.상품카드Id);
        queryGate.SetResult(null);
        await pending;

        Assert.False(f.Session.IsSubmitting);
        Assert.Equal(register ? "source" : "", f.Session.RegisteredDemandSourceKey);
        Assert.Equal(register ? Severity.Warning : Severity.Success, f.Session.StatusSeverity);
        if (register) Assert.StartsWith("상품3 수요는 등록되었지만", f.Session.StatusMessage);
        else Assert.StartsWith("비구속 수요를 철회하고", f.Session.StatusMessage);
    }

    private static Exception Failure(int status) => status == 0 ? new InvalidOperationException("synthetic")
        : new SsalddelApiException("synthetic", status, "test", "", null);
    private static OperatingMarketDeliveryScopeCandidate Scope(string key, bool recommended = false)
        => new() { ScopeKey = key, DisplayName = key, IsRecommendedRecruitmentScope = recommended };
    private static HS먹거리공동구매상품카드 Product(string id, string name)
        => new(id, name, "0201", "식품", "Cold", "Review", 30m, 1000m);

    private sealed class Fixture
    {
        internal GroupPurchaseDemandSession Session { get; } = new();
        internal HS먹거리공동구매상품카드 Product { get; set; } = GroupPurchaseDemandSessionTests.Product("product-1", "상품1");
        internal 공동구매자동집단요약응답 Canonical { get; } = new() { 자동집단Id = "group", 수요건수 = 4, 총희망수량 = 12m, 수량단위 = "kg" };
        internal List<string> Calls { get; } = [];
        internal int AuthenticationRequests { get; private set; }
        internal Func<공동구매자동수요등록Command, Task<공동구매자동집단사용자응답?>> Register { get; set; }
            = _ => Task.FromResult<공동구매자동집단사용자응답?>(new() { 자동집단Id = "group" });
        internal Func<string, string, Task<IReadOnlyList<공동구매자동집단요약응답>?>> List { get; set; }
        internal Func<string, string, Task<공동구매자동수요철회응답?>> Withdraw { get; set; }
            = (_, _) => Task.FromResult<공동구매자동수요철회응답?>(new() { 철회완료 = true, 자동집단Id = "group" });

        internal Fixture()
        {
            Session.ChangeProduct(Product.상품카드Id);
            Session.SelectDeliveryScope(Scope("scope-1"));
            Session.DeliveryScopeConfirmed = true;
            List = (_, _) => Task.FromResult<IReadOnlyList<공동구매자동집단요약응답>?>([Canonical]);
        }

        internal Task RegisterAsync() => Session.RegisterDemandAsync("source", () =>
        {
            Assert.True(Session.IsSubmitting);
            Assert.Null(Session.RegisteredGroup);
            return new() { 수요출처키 = "source", 상품키 = Product.상품카드Id };
        }, request => { Calls.Add("register"); return Register(request); }, Read, () => Product, AuthenticationExpired);
        internal Task WithdrawAsync() => Session.WithdrawDemandAsync((key, reason) =>
        {
            Assert.True(Session.IsSubmitting);
            Assert.Equal("source", key);
            Assert.Equal("주문자 앱에서 철회", reason);
            Calls.Add("withdraw");
            return Withdraw(key, reason);
        }, Read, () => Product, AuthenticationExpired);
        private Task<IReadOnlyList<공동구매자동집단요약응답>?> Read(string product, string scope)
        { Calls.Add("list"); return List(product, scope); }
        private Task AuthenticationExpired() { AuthenticationRequests++; return Task.CompletedTask; }
    }
}
