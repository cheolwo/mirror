using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class SellerCurrentPageEmptyRecoveryTests
{
    private const string EmptyPageTitle = "이 페이지에 표시할 출고 후보가 없습니다.";
    private const string FirstPageLabel = "첫 페이지 조회";

    [Fact]
    public async Task 전체후보는있지만현재페이지만비면_전체0건과구별하고_명시적으로첫페이지를조회한다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        context.Orders.EnqueueResponse(FirstPage());
        var html = await context.StartAsync();

        Assert.Contains(EmptyPageTitle, html);
        Assert.Contains("현재 입력한 검색 조건으로 첫 페이지를 다시 조회해 주세요.", html);
        Assert.DoesNotContain("저장된 주문 출고 후보가 없습니다.", html);
        Assert.DoesNotContain("검색 조건에 맞는 출고 후보가 없습니다.", html);
        Assert.Equal(3, context.Model.목록.현재페이지);
        Assert.Equal(1, context.Model.목록.총페이지수);
        Assert.Single(context.Orders.Requests);
        Assert.Equal(2, context.Orders.Requests[0].Page);

        var button = await context.ButtonAsync(FirstPageLabel);
        await context.ClickAsync(button);

        Assert.Equal(new[] { 2, 0 }, context.Orders.Requests.Select(request => request.Page));
        Assert.Equal("적용한 주문", context.Orders.Requests[1].Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Overseas, context.Orders.Requests[1].SyncScope);
        Assert.Equal("출고예정", context.Orders.Requests[1].Status);
        Assert.Equal(1, context.Model.목록.현재페이지);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
        var recovered = await context.HtmlAsync();
        Assert.Contains("가상 냉장 세트", recovered);
        Assert.DoesNotContain(EmptyPageTitle, recovered);
        Assert.DoesNotContain(FirstPageLabel, recovered);
    }

    [Fact]
    public async Task 첫페이지조회는_안내대로_현재입력한초안조건을보내고_입력을지우지않는다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        context.Orders.EnqueueResponse(FirstPage());
        await context.StartAsync();
        await context.ChangeFiltersAsync("새 주문 B", CommerceChannelOrderSyncScopes.Domestic, "출고준비중");

        Assert.Single(context.Orders.Requests);
        var button = await context.ButtonAsync(FirstPageLabel);
        await context.ClickAsync(button);

        var request = context.Orders.Requests[1];
        Assert.Equal(0, request.Page);
        Assert.Equal(25, request.PageSize);
        Assert.Equal("새 주문 B", request.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, request.SyncScope);
        Assert.Equal("출고준비중", request.Status);
        Assert.Equal("새 주문 B", context.Model.목록.검색어);
        Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, context.Model.목록.국내외구분);
        Assert.Equal("출고준비중", context.Model.목록.출고상태);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
        Assert.Equal(2, context.Orders.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 전체0건은_기존원장없음이나검색결과없음으로표시하고_페이지복구를제안하지않는다(bool filtered)
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(0, 2));
        var html = await context.StartAsync(new()
        {
            [nameof(ShipperSalesOrderWorkspace.InitialSearch)] = filtered ? "없는 주문" : string.Empty,
            [nameof(ShipperSalesOrderWorkspace.InitialSyncScope)] = null,
            [nameof(ShipperSalesOrderWorkspace.InitialStatus)] = null
        });

        Assert.Contains(filtered ? "검색 조건에 맞는 출고 후보가 없습니다." : "저장된 주문 출고 후보가 없습니다.", html);
        Assert.DoesNotContain(EmptyPageTitle, html);
        Assert.DoesNotContain(FirstPageLabel, html);
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 행이있는정상목록은_현재페이지복구안내없이_같은주문의상세진입을유지한다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(FirstPage());
        var html = await context.StartAsync(new() { [nameof(ShipperSalesOrderWorkspace.InitialPage)] = 1 });

        Assert.Contains("가상 주문 42", html);
        Assert.Contains("가상 냉장 세트", html);
        Assert.Contains("상세", html);
        Assert.DoesNotContain(EmptyPageTitle, html);
        Assert.DoesNotContain(FirstPageLabel, html);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 전체가여러페이지면_빈현재페이지에서도_기존페이지선택으로이동할수있다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(100, 2));
        var pageFour = FirstPage();
        pageFour.TotalCount = 100;
        pageFour.Page = 3;
        context.Orders.EnqueueResponse(pageFour);
        var html = await context.StartAsync();

        Assert.Contains(EmptyPageTitle, html);
        var pagination = Assert.IsType<MudPagination>(context.Controls.Pagination);
        Assert.Equal(4, pagination.Count);
        Assert.Equal(3, pagination.Selected);
        Assert.Single(context.Orders.Requests);

        await context.Renderer.Dispatcher.InvokeAsync(() => pagination.SelectedChanged.InvokeAsync(4));

        Assert.Equal(new[] { 2, 3 }, context.Orders.Requests.Select(request => request.Page));
        Assert.Equal("적용한 주문", context.Orders.Requests[1].Search);
        Assert.Equal(4, context.Model.목록.현재페이지);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
        Assert.DoesNotContain(EmptyPageTitle, await context.HtmlAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task 조회실패는_정상빈페이지복구안내로바꾸지않는다(HttpStatusCode status)
    {
        await using var context = new Context();
        context.Orders.EnqueueFailure(status);
        var html = await context.StartAsync();

        Assert.Contains("주문 출고 후보를 불러오지 못했습니다.", html);
        Assert.Contains("다시 시도", html);
        Assert.DoesNotContain(EmptyPageTitle, html);
        Assert.DoesNotContain(FirstPageLabel, html);
        Assert.True(context.Model.목록.오류발생);
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 명시적첫페이지조회가실패하면_기존다시시도로_첫페이지와현재조건을재조회한다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        context.Orders.EnqueueFailure(HttpStatusCode.ServiceUnavailable);
        context.Orders.EnqueueResponse(FirstPage());
        await context.StartAsync();
        await context.ChangeFiltersAsync("재확인할 주문", CommerceChannelOrderSyncScopes.Domestic, "출고준비중");

        await context.ClickAsync(await context.ButtonAsync(FirstPageLabel));
        var failed = await context.HtmlAsync();
        Assert.Contains("주문 출고 후보를 불러오지 못했습니다.", failed);
        Assert.DoesNotContain(EmptyPageTitle, failed);
        Assert.DoesNotContain(FirstPageLabel, failed);
        Assert.Equal(2, context.Orders.Requests.Count);

        await context.ClickAsync(await context.ButtonAsync("다시 시도"));

        Assert.Equal(new[] { 2, 0, 0 }, context.Orders.Requests.Select(request => request.Page));
        Assert.All(context.Orders.Requests.Skip(1), request =>
        {
            Assert.Equal("재확인할 주문", request.Search);
            Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, request.SyncScope);
            Assert.Equal("출고준비중", request.Status);
        });
        Assert.Equal(1, context.Model.목록.현재페이지);
        Assert.Equal("재확인할 주문", context.Model.목록.검색어);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
        Assert.DoesNotContain("주문 출고 후보를 불러오지 못했습니다.", await context.HtmlAsync());
    }

    [Fact]
    public async Task 첫페이지조회중_같은버튼의늦은콜백은_진행중GET을중복하거나취소하지않는다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        var response = context.Orders.EnqueuePending();
        await context.StartAsync();
        var button = await context.ButtonAsync(FirstPageLabel);

        var recovery = context.ClickAsync(button);
        await response.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = await context.HtmlAsync();
        Assert.Contains("영속된 주문 출고 후보를 불러오고 있습니다.", pending);
        Assert.DoesNotContain(EmptyPageTitle, pending);
        await context.ClickAsync(button);

        Assert.Equal(2, context.Orders.Requests.Count);
        Assert.False(context.Orders.LastToken.IsCancellationRequested);
        Assert.False(recovery.IsCompleted);
        response.Response.SetResult(FirstPage());
        await recovery;
        Assert.Equal(2, context.Orders.Requests.Count);
        Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other-seller")]
    public async Task 인증주체가바뀐뒤_이전빈페이지의버튼콜백은_주문을추가조회하지않는다(string? userId)
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        await context.StartAsync();
        var button = await context.ButtonAsync(FirstPageLabel);

        await context.Renderer.Dispatcher.InvokeAsync(() => context.User.현재사용자 = userId is null
            ? 현재사용자Snapshot.익명 : new(userId, "다른 가상 판매자", ["판매자"]));
        await context.ClickAsync(button);

        var html = await context.HtmlAsync();
        Assert.DoesNotContain(EmptyPageTitle, html);
        Assert.DoesNotContain(FirstPageLabel, html);
        Assert.DoesNotContain("sales-order-filters", html);
        Assert.Single(context.Orders.Requests);
    }

    [Fact]
    public async Task 접근재확인중에는_페이지복구를잠시막고_기존같은페이지갱신만진행한다()
    {
        await using var context = new Context();
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        context.Orders.EnqueueResponse(EmptyPage(24, 2));
        await context.StartAsync();
        var button = await context.ButtonAsync(FirstPageLabel);
        var access = context.Access.BlockNext();

        var refresh = context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace.RefreshAsync());
        await context.Access.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(button.Disabled);
        await context.ClickAsync(button);
        Assert.Single(context.Orders.Requests);
        Assert.False(refresh.IsCompleted);

        access.SetResult(true);
        await refresh;
        Assert.Equal(new[] { 2, 2 }, context.Orders.Requests.Select(request => request.Page));
        Assert.Contains(EmptyPageTitle, await context.HtmlAsync());
        Assert.Equal(3, context.Model.목록.현재페이지);
    }

    private static 판매채널주문목록응답 EmptyPage(int totalCount, int page)
        => new() { TotalCount = totalCount, Page = page, PageSize = 25 };

    private static 판매채널주문목록응답 FirstPage()
        => new()
        {
            Items = [new()
            {
                OrderId = 42,
                주문참조번호 = "virtual-order-42",
                채널종류 = "naver",
                채널주문번호 = "가상 주문 42",
                대표상품명 = "가상 냉장 세트",
                총수량 = 6,
                출고라인수 = 2,
                출고창고수 = 1,
                출고창고표시 = "가상 창고",
                출고상태 = "출고예정",
                수정일시 = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc)
            }],
            TotalCount = 24,
            Page = 0,
            PageSize = 25
        };

    private sealed class Context : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        internal readonly OrderService Orders = new();
        internal readonly AccessService Access = new();
        internal readonly CurrentUser User = new();
        internal readonly 판매채널주문PageViewModel Model;
        internal readonly HtmlRenderer Renderer;
        internal readonly CapturedControls Controls;
        internal ShipperSalesOrderWorkspace Workspace { get; private set; } = null!;
        private Func<string> renderHtml = () => string.Empty;

        internal Context()
        {
            Model = new(new(Access, User), new(Orders), new(Orders));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton(Model);
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(new TestNavigation());
            services.AddSingleton<CapturedControls>();
            services.AddSingleton<IComponentActivator>(sp => sp.GetRequiredService<CapturedControls>());
            provider = services.BuildServiceProvider();
            Controls = provider.GetRequiredService<CapturedControls>();
            Renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        internal Task<string> StartAsync(Dictionary<string, object?>? overrides = null)
            => Renderer.Dispatcher.InvokeAsync(async () =>
            {
                await Renderer.RenderComponentAsync<RoleAppProviders>();
                var parameters = new Dictionary<string, object?>
                {
                    [nameof(ShipperSalesOrderWorkspace.InitialSearch)] = "적용한 주문",
                    [nameof(ShipperSalesOrderWorkspace.InitialSyncScope)] = CommerceChannelOrderSyncScopes.Overseas,
                    [nameof(ShipperSalesOrderWorkspace.InitialStatus)] = "출고예정",
                    [nameof(ShipperSalesOrderWorkspace.InitialPage)] = 3
                };
                if (overrides is not null)
                    foreach (var parameter in overrides) parameters[parameter.Key] = parameter.Value;
                var output = await Renderer.RenderComponentAsync<WorkspaceHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(WorkspaceHost.WorkspaceParameters)] = parameters,
                    [nameof(WorkspaceHost.Capture)] = (Action<ShipperSalesOrderWorkspace>)(workspace => Workspace = workspace)
                }));
                renderHtml = () => WebUtility.HtmlDecode(output.ToHtmlString());
                return renderHtml();
            });

        internal Task<string> HtmlAsync() => Renderer.Dispatcher.InvokeAsync(renderHtml);

        internal Task<MudButton> ButtonAsync(string label)
            => Renderer.Dispatcher.InvokeAsync(async () =>
            {
                foreach (var button in Controls.Buttons.ToArray().Reverse())
                {
                    if (button.ChildContent is null) continue;
                    var output = await Renderer.RenderComponentAsync<FragmentHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                    {
                        [nameof(FragmentHost.ChildContent)] = button.ChildContent
                    }));
                    if (string.Equals(WebUtility.HtmlDecode(output.ToHtmlString()).Trim(), label, StringComparison.Ordinal))
                        return button;
                }
                throw new Xunit.Sdk.XunitException($"실제 렌더에서 '{label}' 버튼을 찾지 못했습니다.");
            });

        internal Task ClickAsync(MudButton button)
            => Renderer.Dispatcher.InvokeAsync(() => button.OnClick.InvokeAsync(new MouseEventArgs()));

        internal Task ChangeFiltersAsync(string search, string? scope, string? status)
            => Renderer.Dispatcher.InvokeAsync(async () =>
            {
                await Assert.Single(Controls.TextFields.Where(field => field.Label == "주문번호·상품·SKU")).ValueChanged.InvokeAsync(search);
                await Assert.Single(Controls.Selects.Where(select => select.Label == "국내·해외")).ValueChanged.InvokeAsync(scope);
                await Assert.Single(Controls.Selects.Where(select => select.Label == "출고 상태")).ValueChanged.InvokeAsync(status);
            });

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

    private sealed class FragmentHost : ComponentBase
    {
        public FragmentHost() { }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
    }

    private sealed class CapturedControls(IServiceProvider services) : IComponentActivator
    {
        internal List<MudButton> Buttons { get; } = [];
        internal List<MudTextField<string>> TextFields { get; } = [];
        internal List<MudSelect<string>> Selects { get; } = [];
        internal MudPagination? Pagination { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
            if (component is MudButton button) Buttons.Add(button);
            if (component is MudTextField<string> textField) TextFields.Add(textField);
            if (component is MudSelect<string> select) Selects.Add(select);
            if (component is MudPagination pagination) Pagination = pagination;
            return component;
        }
    }

    private sealed class AccessService : I판매채널페이지접근Service
    {
        internal TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<bool>? nextResponse;
        internal TaskCompletionSource<bool> BlockNext()
        {
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return nextResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public async Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nextResponse is not { } response) return true;
            nextResponse = null;
            Started.TrySetResult();
            return await response.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CurrentUser : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        private 현재사용자Snapshot current = new("seller", "가상 판매자", ["판매자"]);
        public event Action? Changed;
        public 현재사용자Snapshot 현재사용자
        {
            get => current;
            set { current = value; Changed?.Invoke(); }
        }
    }

    private sealed class OrderService : I판매채널주문읽기Service
    {
        internal List<판매채널주문목록조회요청> Requests { get; } = [];
        internal CancellationToken LastToken;
        private readonly Queue<Func<CancellationToken, Task<판매채널주문목록응답>>> responses = new();
        internal void EnqueueResponse(판매채널주문목록응답 response)
            => responses.Enqueue(_ => Task.FromResult(response));
        internal void EnqueueFailure(HttpStatusCode status)
            => responses.Enqueue(_ => Task.FromException<판매채널주문목록응답>(new HttpRequestException("Isolated order read failure", null, status)));
        internal PendingRead EnqueuePending()
        {
            var pending = new PendingRead();
            responses.Enqueue(async token =>
            {
                pending.Started.TrySetResult();
                return await pending.Response.Task.WaitAsync(token);
            });
            return pending;
        }
        public Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            LastToken = cancellationToken;
            if (responses.Count == 0) throw new InvalidOperationException("예상하지 않은 추가 목록 조회입니다.");
            return responses.Dequeue()(cancellationToken);
        }
        public Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널주문상세응답?>(null);
    }

    private sealed class PendingRead
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<판매채널주문목록응답> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
