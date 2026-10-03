using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantReceiptRecoveryTests
{
    [Fact]
    public async Task FreshProcessRestoresAcceptedOrderAndReprintsWithoutReaccepting()
    {
        var api = new Orders();
        var desk = Desk(api);
        var result = await desk.전표준비Async("A");
        Assert.True(result.성공);
        Assert.Equal("A", result.전표Draft!.DocumentNo);
        Assert.Equal("새 메뉴", Assert.Single(result.전표Draft.Lines).ProductName);
        Assert.Equal("기사배정", result.주문!.상태);
        Assert.Equal(1, api.Reads);
        Assert.Equal(0, api.Commands);
    }

    [Theory]
    [InlineData("취소", true)]
    [InlineData("거절", true)]
    [InlineData("주문대기", false)]
    public async Task UnacceptedOrClosedOrderCannotProduceAUsableReceipt(string status, bool accepted)
    {
        var api = new Orders();
        api.Current.상태 = status;
        if (!accepted) api.Current.음식점수락시각Utc = null;
        var result = await Desk(api).전표준비Async("A");
        Assert.False(result.성공);
        Assert.Null(result.전표Draft);
        Assert.Equal(status, result.주문!.상태);
        Assert.Equal(0, api.Commands);
    }

    [Fact]
    public async Task RepeatedPreparationAlwaysReadsLatestServerMenuAndRevision()
    {
        var api = new Orders();
        var desk = Desk(api);
        var first = await desk.전표준비Async("A");
        api.Current = Detail("조리중", "변경 메뉴", 8);
        var second = await desk.전표준비Async("A");
        Assert.Equal("새 메뉴", Assert.Single(first.전표Draft!.Lines).ProductName);
        Assert.Equal("변경 메뉴", Assert.Single(second.전표Draft!.Lines).ProductName);
        Assert.Equal(8, second.상세주문!.Revision);
        Assert.Equal(2, api.Reads);
        Assert.Equal(0, api.Commands);
    }

    [Fact]
    public async Task PrintDialogRecordDoesNotReplaceServerBusinessStatusOrClaimPhysicalCompletion()
    {
        var api = new Orders();
        var desk = Desk(api);
        var result = await desk.전표준비Async("A");
        await desk.전표출력요청기록Async("A");
        Assert.Equal("기사배정", result.주문!.상태);
        Assert.NotNull(result.주문.전표출력요청시각);
        Assert.Null(result.주문.전표출력시각);
        Assert.Equal(0, api.Commands);
        // The legacy method remains compatible but carries the same request-only semantics.
        await desk.전표출력완료Async("A");
        Assert.Equal("기사배정", result.주문.상태);
        Assert.Null(result.주문.전표출력시각);
    }

    [Fact]
    public async Task OfflinePreparationCannotUseStaleCachedReceiptOrReaccept()
    {
        var api = new Orders();
        var desk = Desk(api);
        await desk.전표준비Async("A");
        api.Read = (_, _) => throw new HttpRequestException("controlled offline");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.전표준비Async("A"));
        Assert.Equal(0, api.Commands);
    }

    [Fact]
    public async Task PrintDialogFailureAllowsIndependentRetryWithoutDuplicateAcceptance()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Printer.FailNext = true;
        await f.CallAsync("PrintReceiptAsync");
        Assert.Contains("주문은 다시 수락되지", f.Get<string>("message"));
        Assert.False(f.Get<bool>("readFailed"));
        Assert.Equal(0, f.Desk.Requests);
        await f.CallAsync("PrintReceiptAsync");
        Assert.Equal(2, f.Desk.Prepares);
        Assert.Equal(2, f.Printer.Calls);
        Assert.Equal(1, f.Desk.Requests);
        Assert.Contains("실제 인쇄 결과", f.Get<string>("message"));
        Assert.Equal(0, f.Desk.Acceptances);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LatePreparedReceiptCannotPrintAfterAccountOrSelectionChanges(bool selectionChange)
    {
        await using var f = await PageFixture.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<음식점주문수락결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Desk.Prepare = _ => { started.SetResult(); return result.Task; };
        var print = f.CallAsync("PrintReceiptAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (selectionChange) await f.MountAsync("B");
        else await f.Session.ApplyAsync(Token("other-owner"));
        result.SetResult(Prepared("A"));
        await print;
        Assert.Equal(0, f.Printer.Calls);
        Assert.Equal(0, f.Desk.Requests);
        Assert.Equal(0, f.Desk.Acceptances);
    }

    [Fact]
    public async Task ReceiptAuthenticationFailureLocksWorkAndRoutesToLogin()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Desk.Prepare = _ => throw new UnauthorizedAccessException("controlled rejection");
        await f.CallAsync("PrintReceiptAsync");
        Assert.True(f.Get<bool>("workLocked"));
        Assert.Null(f.Get<음식점주문DeskItem?>("order"));
        Assert.EndsWith("/login", f.Navigation.Uri);
        Assert.Equal(0, f.Printer.Calls);
    }

    private static 음식점주문DeskService Desk(Orders api) => new(api, new Sound(), new 음식점전표DraftFactory(),
        new Preparation(), Options.Create(new RestaurantDeskOptions()));
    private static 음식주문응답 Detail(string status = "기사배정", string menu = "새 메뉴", long revision = 7)
        => new()
        {
            주문번호 = "A", 음식점Id = 1, 상태 = status, Revision = revision,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20), 음식점수락시각Utc = DateTime.UtcNow.AddMinutes(-10),
            상품목록 = [new() { 상품명 = menu, 수량 = 2, 단가 = 9000 }],
            수령인정보 = new() { 수령인명 = "합성 수령인", 연락처 = "01000000000", 주소 = "합성 주소" }
        };
    private static 음식점주문수락결과 Prepared(string orderNo)
    {
        var detail = Detail(); detail.주문번호 = orderNo;
        return new() { 성공 = true, 주문 = new() { 주문번호 = orderNo, 상태 = "기사배정", 상세주문 = detail },
            상세주문 = detail, 전표Draft = new 음식점전표DraftFactory().Create주문전표Draft(detail) };
    }
    private static ClientAuthTokenSnapshot Token(string owner) => new("test-access", DateTime.UtcNow.AddHours(1),
        "test-refresh", DateTime.UtcNow.AddDays(1), owner, "test", ["음식점"]);
    private sealed class Orders : I음식주문ApiClient
    {
        public 음식주문응답 Current = Detail();
        public int Reads, Commands;
        public Func<string, CancellationToken, Task<음식주문응답?>>? Read;
        public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default)
        { Reads++; return Read?.Invoke(주문번호, cancellationToken) ?? Task.FromResult<음식주문응답?>(Current); }
        public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default) { Commands++; throw new NotSupportedException(); }
        public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default) { Commands++; throw new NotSupportedException(); }
    }
    private sealed class Preparation : I음식점조리시간설정Service
    {
        public 음식점조리시간설정Snapshot 현재조회() => new(20, new Dictionary<string, int>());
        public Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Sound : I주문알림Service
    { public Task 신규주문알림재생Async(CancellationToken cancellationToken = default) => Task.CompletedTask; }

#pragma warning disable BL0006
    private sealed class PageFixture : IAsyncDisposable
    {
        public ClientAuthSession Session { get; } = new(new Store(), new ClientSessionGuard());
        public PageDesk Desk { get; } = new();
        public Printer Printer { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public OrderDetail Page { get; }
        private readonly TestRenderer renderer = new();
        private int? id;
        private PageFixture() => Page = new()
        {
            OrderDeskService = Desk, ReceiptPrinter = Printer, NavigationManager = Navigation,
            RestaurantRealtimeService = new Realtime(), DocumentOutputService = new Documents(),
            AuthService = new RestaurantAuthService(new HttpClient(), Session)
        };
        public static async Task<PageFixture> CreateAsync()
        {
            var f = new PageFixture(); await f.Session.ApplyAsync(Token("owner")); await f.MountAsync("A"); return f;
        }
        public Task MountAsync(string orderNo) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            id ??= renderer.Add(Page);
            await renderer.MountAsync(id.Value, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(OrderDetail.OrderNo)] = orderNo }));
        });
        public T Get<T>(string name) => (T)typeof(OrderDetail).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Page)!;
        public Task CallAsync(string name) => renderer.Dispatcher.InvokeAsync(() => (Task)typeof(OrderDetail).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Page, null)!);
        public ValueTask DisposeAsync() => renderer.DisposeAsync();
    }
    private sealed class TestRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public int Add(IComponent component) => AssignRootComponentId(component);
        public Task MountAsync(int id, ParameterView parameters) => RenderRootComponentAsync(id, parameters);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
    }
#pragma warning restore BL0006
    private sealed class Store : IClientSecureTokenStore
    {
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<ClientAuthTokenSnapshot?>(null);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Printer : IRestaurantReceiptPrinter
    {
        public bool UsesNativeDialog => true;
        public int Calls;
        public bool FailNext;
        public Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
        { Calls++; if (FailNext) { FailNext = false; throw new InvalidOperationException("controlled unavailable print service"); } return Task.CompletedTask; }
    }
    private sealed class Documents : ISsalddelDocumentOutputService
    {
        public SsalddelDocumentOutput CreateOutboundExpectedItems(SsalddelExpectedItemDocumentDraft draft) => new("test.html", draft.Title, "text/html", "<p>synthetic receipt</p>", "synthetic receipt");
        public SsalddelDocumentOutput CreateInboundExpectedItems(SsalddelExpectedItemDocumentDraft draft) => throw new NotSupportedException();
        public SsalddelDocumentOutput CreateWaybill(SsalddelWaybillDocumentDraft draft) => throw new NotSupportedException();
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/A");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
        protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class Realtime : I음식점주문SignalRClientService
    {
        public event Func<음식점주문수신알림, Task>? 주문수신 { add { } remove { } }
        public event Func<음식점주문상태변경알림, Task>? 주문상태변경 { add { } remove { } }
        public event Func<음식점실시간연결상태변경, Task>? 상태변경 { add { } remove { } }
        public event Func<Task>? 재연결후재조회요청 { add { } remove { } }
        public 음식점실시간연결상태 연결상태 => 음식점실시간연결상태.연결됨;
        public Task 연결Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 연결해제Async() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class PageDesk : I음식점주문DeskService
    {
        public int Prepares, Acceptances, Requests;
        public Func<CancellationToken, Task<음식점주문수락결과>>? Prepare;
        public Task<음식점주문DeskItem?> 주문조회Async(string 주문번호, 음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default) => Task.FromResult<음식점주문DeskItem?>(Prepared(주문번호).주문);
        public Task<음식점주문수락결과> 전표준비Async(string 주문번호, CancellationToken cancellationToken = default) { Prepares++; return Prepare?.Invoke(cancellationToken) ?? Task.FromResult(Prepared(주문번호)); }
        public Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default) { Requests++; return Task.CompletedTask; }
        public Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, CancellationToken cancellationToken = default) { Acceptances++; throw new NotSupportedException(); }
        public Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default) { Acceptances++; throw new NotSupportedException(); }
        public Task<음식점주문DeskItem?> 주문거절Async(string 주문번호, string 사유, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem?> 조리시간변경Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem?> 픽업준비완료Async(string 주문번호, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
