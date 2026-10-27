using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class SellerOrderRecoveryUsabilityTests
{
    [Fact]
    public async Task 같은목록갱신은_변경한검색조건과현재페이지를_유지한다()
    {
        await using var context = new Context();
        await context.StartAsync(new()
        {
            [nameof(ShipperSalesOrderWorkspace.InitialSearch)] = "초기 검색",
            [nameof(ShipperSalesOrderWorkspace.InitialPage)] = 2
        });

        await context.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            context.Model.목록.검색어 = "계속 작성한 검색";
            context.Model.목록.국내외구분 = CommerceChannelOrderSyncScopes.Overseas;
            context.Model.목록.출고상태 = "출고예정";
            await context.Workspace.RefreshAsync();
        });

        Assert.Equal(2, context.Orders.Requests.Count);
        var request = context.Orders.Requests[^1];
        Assert.Equal("계속 작성한 검색", request.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Overseas, request.SyncScope);
        Assert.Equal("출고예정", request.Status);
        Assert.Equal(1, request.Page);
        Assert.Equal(2, context.Model.목록.현재페이지);
        Assert.Equal(2, context.Access.Calls);
    }

    [Fact]
    public async Task 같은계정_접근재확인중에도_검색입력구성을_유지하고_조회는잠시막는다()
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Model.목록.검색어 = "작성 중 검색";
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var pending = await context.HtmlAsync();
        Assert.Contains("sales-order-filters", pending);
        Assert.Contains("작성 중 검색", pending);
        Assert.DoesNotContain("판매채널 기능과 주문 원장 접근 권한을 확인하고 있습니다.", pending);
        Assert.Contains("disabled", pending);
        Assert.Single(context.Orders.Requests);

        response.SetResult(true);
        await refresh;
        Assert.Equal(2, context.Orders.Requests.Count);
        Assert.Equal("작성 중 검색", context.Orders.Requests[^1].Search);
        Assert.Contains("sales-order-filters", await context.HtmlAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 접근재확인_OFF또는오류는_기존목록과검색화면을_차단한다(bool fails)
    {
        await using var context = new Context();
        await context.StartAsync();
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (fails) response.SetException(new HttpRequestException("접근 상태 조회 실패"));
        else response.SetResult(false);
        await refresh;

        var html = await context.HtmlAsync();
        Assert.DoesNotContain("sales-order-filters", html);
        Assert.Single(context.Orders.Requests);
        Assert.Contains(fails ? "화면을 준비하지 못했습니다." : "기능이 현재 비활성입니다.", html);
    }

    [Theory]
    [InlineData("other-seller", "판매자")]
    [InlineData("seller", "화주")]
    [InlineData(null, "")]
    [InlineData("seller", "주문자")]
    public async Task 계정이나역할변경은_재확인중과완료후_이전입력을_노출하지않는다(string? userId, string role)
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Model.목록.검색어 = "이전 계정 검색";
        context.User.현재사용자 = userId is null ? 현재사용자Snapshot.익명 : new(userId, "바뀐 계정", [role]);
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.DoesNotContain("sales-order-filters", await context.HtmlAsync());
        response.SetResult(true);
        await refresh;
        var html = await context.HtmlAsync();
        Assert.DoesNotContain("sales-order-filters", html);
        Assert.DoesNotContain("이전 계정 검색", html);
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 접근재확인중_화면종료는_입력유지모드를끝내고_진행작업을취소한다()
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.Dispose());
        await refresh;

        Assert.True(context.Access.LastToken.IsCancellationRequested);
        Assert.Single(context.Orders.Requests);
    }

    [Theory]
    [InlineData("other-seller", "판매자")]
    [InlineData("seller", "주문자")]
    [InlineData(null, "")]
    public async Task 재확인응답을_기다리는동안_인증변경신호만으로_이전자료를_즉시숨긴다(string? userId, string role)
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Model.목록.검색어 = "이전 계정 작성값";
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("sales-order-filters", await context.HtmlAsync());

        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = userId is null
            ? 현재사용자Snapshot.익명 : new(userId, "변경한 계정", [role]));
        var pending = await context.HtmlAsync();
        Assert.DoesNotContain("sales-order-filters", pending);
        Assert.DoesNotContain("이전 계정 작성값", pending);
        Assert.False(refresh.IsCompleted);
        Assert.Single(context.Orders.Requests);

        response.SetResult(true);
        await refresh;
        Assert.DoesNotContain("sales-order-filters", await context.HtmlAsync());
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 같은계정의_토큰갱신신호는_재확인중_입력과조회조건을_유지한다()
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Model.목록.검색어 = "토큰 갱신 중 초안";
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = new("seller", "같은 계정", ["판매자"]));

        Assert.Contains("sales-order-filters", await context.HtmlAsync());
        Assert.Equal("토큰 갱신 중 초안", context.Model.목록.검색어);
        response.SetResult(true);
        await refresh;
        Assert.Equal("토큰 갱신 중 초안", context.Orders.Requests[^1].Search);
        Assert.Equal(2, context.Orders.Requests.Count);
    }

    [Fact]
    public async Task 재확인중_계정이바뀌었다돌아와도_명시적재조회전에는_이전자료를_재노출하지않는다()
    {
        await using var context = new Context();
        await context.StartAsync();
        var response = context.Access.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = new("other-seller", "다른 계정", ["판매자"]));
        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = new("seller", "원래 계정", ["판매자"]));
        Assert.DoesNotContain("sales-order-filters", await context.HtmlAsync());

        response.SetResult(true);
        await refresh;
        Assert.DoesNotContain("sales-order-filters", await context.HtmlAsync());
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 화면생명주기에서_인증변경은_한번만구독하고_종료때해제한다()
    {
        await using var context = new Context();
        await context.StartAsync();
        Assert.Equal(1, context.User.SubscriberCount);
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        Assert.Equal(1, context.User.SubscriberCount);

        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.Dispose());
        Assert.Equal(0, context.User.SubscriberCount);
        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = 현재사용자Snapshot.익명);
        Assert.Equal(3, context.Orders.Requests.Count);
    }

    [Fact]
    public void 공통현재사용자Context는_기존토큰저장소의_변경신호와구독해제를_전달한다()
    {
        var services = new ServiceCollection();
        var tokens = new TokenChanges();
        services.AddSingleton(tokens);
        services.AddSsalddelCommunityWritingServices<TokenChanges>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ISsalddel현재사용자Context>();
        var changes = Assert.IsAssignableFrom<ISsalddel현재사용자변경알림>(user);
        var notifications = 0;
        Action handler = () => notifications++;
        changes.Changed += handler;
        Assert.Equal(1, tokens.SubscriberCount);

        tokens.Apply("seller", "판매자");
        Assert.Equal("seller", user.현재사용자.UserId);
        Assert.True(user.현재사용자.역할보유("판매자"));
        tokens.Apply("seller", "주문자");
        Assert.True(user.현재사용자.역할보유("주문자"));
        tokens.Apply(null, null);
        Assert.False(user.현재사용자.인증됨);
        Assert.Equal(3, notifications);

        changes.Changed -= handler;
        Assert.Equal(0, tokens.SubscriberCount);
        tokens.Apply("other-seller", "판매자");
        Assert.Equal(3, notifications);
    }

    [Fact]
    public async Task 사용자조회중_주기갱신은_진행중인조회를_중복하거나취소하지않는다()
    {
        await using var context = new Context();
        await context.StartAsync();
        var response = context.Orders.BlockNext();
        var userRead = context.Renderer.Dispatcher.InvokeAsync(() => context.Model.목록.페이지조회Async(3));
        await context.Orders.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());

        Assert.Equal(2, context.Orders.Requests.Count);
        Assert.Equal(1, context.Access.Calls);
        Assert.False(context.Orders.LastToken.IsCancellationRequested);
        response.SetResult(new() { TotalCount = 100, Page = 2, PageSize = 25 });
        Assert.True(await userRead);
        Assert.Equal(3, context.Model.목록.현재페이지);
    }

    [Fact]
    public async Task 화면종료는_진행중갱신을취소하고_늦은주기신호를_무시한다()
    {
        await using var context = new Context();
        await context.StartAsync();
        context.Orders.BlockNext();
        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Orders.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.Dispose());
        await refresh;
        await context.Workspace.RefreshAsync();

        Assert.True(context.Orders.LastToken.IsCancellationRequested);
        Assert.Equal(2, context.Orders.Requests.Count);
        Assert.Equal(2, context.Access.Calls);
    }

    [Theory]
    [InlineData(false, "seller", "판매자")]
    [InlineData(true, null, "")]
    [InlineData(true, "orderer", "주문자")]
    public async Task 판매자소비자의_접근복구는_존재하는홈과로그인으로만_연결한다(
        bool enabled, string? userId, string role)
    {
        await using var context = new Context(enabled, userId, role);
        var html = await context.StartAsync(new()
        {
            [nameof(ShipperSalesOrderWorkspace.HomeHref)] = "/",
            [nameof(ShipperSalesOrderWorkspace.HomeLabel)] = "판매자 홈으로",
            [nameof(ShipperSalesOrderWorkspace.PublicHref)] = "/",
            [nameof(ShipperSalesOrderWorkspace.PublicLabel)] = "판매자 홈으로"
        });

        Assert.Contains("href=\"/\"", html);
        Assert.Contains("판매자 홈으로", html);
        Assert.DoesNotContain("href=\"/shipper\"", html);
        Assert.DoesNotContain("href=\"/community\"", html);
        if (enabled && userId is null) Assert.Contains("href=\"/login\"", html);
        Assert.Empty(context.Orders.Requests);
    }

    [Fact]
    public async Task 다른공용소비자는_기본화주홈복귀를_유지한다()
    {
        await using var context = new Context(enabled: false);
        var html = await context.StartAsync();
        Assert.Contains("href=\"/shipper\"", html);
        Assert.Contains("화주 홈으로", html);
        Assert.Empty(context.Orders.Requests);
    }

    private sealed class Context : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        internal readonly OrderService Orders = new();
        internal readonly AccessService Access;
        internal readonly CurrentUser User;
        internal readonly 판매채널주문PageViewModel Model;
        internal readonly HtmlRenderer Renderer;
        internal ShipperSalesOrderWorkspace Workspace { get; private set; } = null!;
        private Func<string> renderHtml = () => string.Empty;

        internal Context(bool enabled = true, string? userId = "seller", string role = "판매자")
        {
            Access = new(enabled);
            User = new CurrentUser(userId is null ? 현재사용자Snapshot.익명 : new(userId, "계정", [role]));
            Model = new(new(Access, User), new(Orders), new(Orders));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton(Model);
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(new TestNavigation());
            provider = services.BuildServiceProvider();
            Renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        internal Task<string> StartAsync(Dictionary<string, object?>? parameters = null)
            => Renderer.Dispatcher.InvokeAsync(async () =>
            {
                await Renderer.RenderComponentAsync<RoleAppProviders>();
                var output = await Renderer.RenderComponentAsync<WorkspaceHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(WorkspaceHost.WorkspaceParameters)] = parameters ?? new(),
                    [nameof(WorkspaceHost.Capture)] = (Action<ShipperSalesOrderWorkspace>)(workspace => Workspace = workspace)
                }));
                renderHtml = () => WebUtility.HtmlDecode(output.ToHtmlString());
                return renderHtml();
            });

        internal Task<string> HtmlAsync() => Renderer.Dispatcher.InvokeAsync(renderHtml);

        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class WorkspaceHost : ComponentBase
    {
        public WorkspaceHost() { }

        [Parameter] public Dictionary<string, object?> WorkspaceParameters { get; set; } = new();
        [Parameter] public Action<ShipperSalesOrderWorkspace>? Capture { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ShipperSalesOrderWorkspace>(0);
            builder.AddMultipleAttributes(1, WorkspaceParameters);
            builder.AddComponentReferenceCapture(2, value => Capture?.Invoke((ShipperSalesOrderWorkspace)value));
            builder.CloseComponent();
        }
    }

    private sealed class AccessService(bool enabled) : I판매채널페이지접근Service
    {
        internal int Calls;
        internal CancellationToken LastToken;
        internal TaskCompletionSource ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<bool>? nextResponse;

        internal TaskCompletionSource<bool> BlockNext()
        {
            ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return nextResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (nextResponse is { } response)
            {
                nextResponse = null;
                ReadStarted.TrySetResult();
                return await response.Task.WaitAsync(cancellationToken);
            }
            return enabled;
        }
    }

    private sealed class CurrentUser(현재사용자Snapshot user) : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        private 현재사용자Snapshot current = user;
        public event Action? Changed;
        internal int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
        public 현재사용자Snapshot 현재사용자
        {
            get => current;
            set { current = value; Changed?.Invoke(); }
        }
    }

    private sealed class TokenChanges : ISsalddelAccessTokenProvider, ISsalddel현재사용자변경알림
    {
        public string? AccessToken { get; private set; }
        public event Action? Changed;
        internal int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
        internal void Apply(string? userId, string? role)
        {
            AccessToken = userId is null ? null : "e30." + Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new { sub = userId, roles = new[] { role } })).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".preview";
            Changed?.Invoke();
        }
    }

    private sealed class OrderService : I판매채널주문읽기Service
    {
        internal readonly List<판매채널주문목록조회요청> Requests = [];
        internal readonly TaskCompletionSource ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken LastToken;
        private TaskCompletionSource<판매채널주문목록응답>? nextResponse;

        internal TaskCompletionSource<판매채널주문목록응답> BlockNext()
            => nextResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            LastToken = cancellationToken;
            if (nextResponse is { } response)
            {
                nextResponse = null;
                ReadStarted.TrySetResult();
                return await response.Task.WaitAsync(cancellationToken);
            }
            return new() { TotalCount = 100, Page = request.Page, PageSize = 25 };
        }

        public Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널주문상세응답?>(null);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/shipper/sales/orders");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
