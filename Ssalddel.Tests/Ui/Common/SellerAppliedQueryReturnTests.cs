using System.Net;
using System.Text.RegularExpressions;
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

public sealed class SellerAppliedQueryReturnTests
{
    private const string AppliedSearch = "A 상품";
    private const string DraftSearch = "B 미조회 초안";
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task A조건2페이지를본뒤_B만입력해도_기존42상세의목록복귀주소는_A조건2페이지다()
    {
        await using var context = new Context();
        await context.StartAsync();
        AssertAppliedReturn(await context.ReturnContextAsync());
        var originalRow = Assert.Single(context.Model.목록.주문목록);

        await context.ChangeToDraftBAsync();

        Assert.Single(context.Orders.Requests);
        AssertRequestA(context.Orders.Requests[0]);
        Assert.Same(originalRow, Assert.Single(context.Model.목록.주문목록));
        Assert.Equal(42, originalRow.OrderId);
        Assert.Equal(12, originalRow.총수량);
        Assert.Equal(DraftSearch, context.Model.목록.검색어);
        Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, context.Model.목록.국내외구분);
        Assert.Equal("출고준비중", context.Model.목록.출고상태);
        AssertAppliedReturn(await context.ReturnContextAsync());
        Assert.Empty(context.Orders.DetailRequests);
    }

    [Fact]
    public async Task A조건재조회대기중_B를입력해도_A응답의42상세주소는_요청시점조건으로복귀한다()
    {
        await using var context = new Context();
        await context.StartAsync();
        var pending = context.Orders.DeferNext();
        var refresh = context.ClickAsync(await context.ButtonAsync("새로고침"));
        await pending.Started.Task.WaitAsync(SignalTimeout);
        try
        {
            Assert.Equal(2, context.Orders.Requests.Count);
            AssertRequestA(context.Orders.Requests[1]);
            await context.ChangeToDraftBAsync();
            Assert.Equal(2, context.Orders.Requests.Count);
            Assert.False(refresh.IsCompleted);

            pending.Release();
            await refresh.WaitAsync(SignalTimeout);

            Assert.Equal(DraftSearch, context.Model.목록.검색어);
            Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, context.Model.목록.국내외구분);
            Assert.Equal("출고준비중", context.Model.목록.출고상태);
            Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);
            Assert.Equal(2, context.Model.목록.현재페이지);
            AssertAppliedReturn(await context.ReturnContextAsync());
            Assert.Equal(2, context.Orders.Requests.Count);
            Assert.Empty(context.Orders.DetailRequests);
        }
        finally
        {
            pending.Release();
            await refresh.WaitAsync(SignalTimeout);
        }
    }

    [Theory]
    [InlineData("조회", 0)]
    [InlineData("새로고침", 1)]
    public async Task 현재입력B의_명시조회성공후에만_상세복귀조건을_B와응답페이지로바꾼다(string label, int requestedPage)
    {
        await using var context = new Context();
        await context.StartAsync();
        await context.ChangeToDraftBAsync();
        Assert.Single(context.Orders.Requests);
        var pending = context.Orders.DeferNext();
        var read = context.ClickAsync(await context.ButtonAsync(label));
        try
        {
            await pending.Started.Task.WaitAsync(SignalTimeout);
            Assert.Equal(2, context.Orders.Requests.Count);
            AssertDraftRequest(context.Orders.Requests[1], DraftSearch, requestedPage);
            AssertAppliedReturn(context.Model.목록.적용된목록문맥);
            Assert.Equal(42, Assert.Single(context.Model.목록.주문목록).OrderId);

            // 새로고침의 요청 page=1에도 서버가 page=0을 응답하면 실제 응답 페이지로 복귀한다.
            pending.Release(DraftResponse());
            await read.WaitAsync(SignalTimeout);

            Assert.Equal(41, Assert.Single(context.Model.목록.주문목록).OrderId);
            Assert.Equal(8, Assert.Single(context.Model.목록.주문목록).총수량);
            Assert.Equal(1, context.Model.목록.현재페이지);
            Assert.Equal(DraftSearch, context.Model.목록.검색어);
            AssertDraftReturn(await context.ReturnContextAsync(41), DraftSearch);
            Assert.Equal(2, context.Orders.Requests.Count);
            Assert.Empty(context.Orders.DetailRequests);
        }
        finally
        {
            pending.Release();
            await read.WaitAsync(SignalTimeout);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B조회실패나협조취소와_중복차단은_A문맥을보존하고_재조회는현재C조건을쓴다(bool cancelled)
    {
        const string retrySearch = "C 재시도";
        await using var context = new Context();
        await context.StartAsync();
        var originalRow = Assert.Single(context.Model.목록.주문목록);
        await context.ChangeToDraftBAsync();
        var pending = context.Orders.DeferNext();
        var read = context.Renderer.Dispatcher.InvokeAsync(() => context.Model.목록.페이지조회Async(4));
        try
        {
            await pending.Started.Task.WaitAsync(SignalTimeout);
            AssertDraftRequest(context.Orders.Requests[1], DraftSearch, 3);
            await context.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                context.Model.목록.검색어 = retrySearch;
                Assert.False(await context.Model.목록.페이지조회Async(9));
            });
            Assert.Equal(2, context.Orders.Requests.Count);
            AssertAppliedReturn(context.Model.목록.적용된목록문맥);

            if (cancelled)
                await context.Renderer.Dispatcher.InvokeAsync(() => context.Model.목록.작업취소());
            else
                pending.Response.TrySetException(new HttpRequestException("가상 조회 실패", null, HttpStatusCode.ServiceUnavailable));
            Assert.False(await read.WaitAsync(SignalTimeout));

            Assert.Equal(cancelled, context.Model.목록.취소됨);
            Assert.Equal(!cancelled, context.Model.목록.오류발생);
            Assert.Same(originalRow, Assert.Single(context.Model.목록.주문목록));
            Assert.Equal(2, context.Model.목록.현재페이지);
            Assert.Equal(cancelled ? 2 : 4, context.Model.목록.재조회페이지);
            AssertAppliedReturn(context.Model.목록.적용된목록문맥);

            var retry = context.Orders.DeferNext();
            var retryRead = context.ClickAsync(await context.ButtonAsync(cancelled ? "새로고침" : "다시 시도"));
            try
            {
                await retry.Started.Task.WaitAsync(SignalTimeout);
                Assert.Equal(3, context.Orders.Requests.Count);
                AssertDraftRequest(context.Orders.Requests[2], retrySearch, cancelled ? 1 : 3);
                AssertAppliedReturn(context.Model.목록.적용된목록문맥);
                retry.Release(DraftResponse(retrySearch));
                await retryRead.WaitAsync(SignalTimeout);

                AssertDraftReturn(await context.ReturnContextAsync(41), retrySearch);
                Assert.Equal(retrySearch, context.Model.목록.검색어);
                Assert.Equal(3, context.Orders.Requests.Count);
                Assert.Empty(context.Orders.DetailRequests);
            }
            finally
            {
                retry.Release();
                await retryRead.WaitAsync(SignalTimeout);
            }
        }
        finally
        {
            pending.Release();
            await read.WaitAsync(SignalTimeout);
        }
    }

    private static void AssertDraftRequest(판매채널주문목록조회요청 request, string search, int page)
    {
        Assert.Equal(search, request.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, request.SyncScope);
        Assert.Equal("출고준비중", request.Status);
        Assert.Equal(page, request.Page);
        Assert.Equal(25, request.PageSize);
    }

    private static void AssertDraftReturn(SalesOrderNavigationContext context, string search)
    {
        Assert.Equal(search, context.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Domestic, context.SyncScope);
        Assert.Equal("출고준비중", context.Status);
        Assert.Equal(1, context.Page);
    }

    private static void AssertRequestA(판매채널주문목록조회요청 request)
    {
        Assert.Equal(AppliedSearch, request.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Overseas, request.SyncScope);
        Assert.Equal("출고예정", request.Status);
        Assert.Equal(1, request.Page);
        Assert.Equal(25, request.PageSize);
    }

    private static void AssertAppliedReturn(SalesOrderNavigationContext context)
    {
        Assert.Equal(AppliedSearch, context.Search);
        Assert.Equal(CommerceChannelOrderSyncScopes.Overseas, context.SyncScope);
        Assert.Equal("출고예정", context.Status);
        Assert.Equal(2, context.Page);
    }

    private static 판매채널주문목록응답 AppliedResponse() => new()
    {
        Items = [new()
        {
            OrderId = 42, 주문참조번호 = "AMAZON-A-42", 채널종류 = CommerceChannelKeys.Amazon,
            채널주문번호 = "VIRTUAL-A-42", 국내외구분 = CommerceChannelOrderSyncScopes.Overseas,
            대표상품명 = "A 상품 출고 후보", 총수량 = 12, 출고라인수 = 2, 출고창고수 = 1,
            출고창고표시 = "가상 출고센터", 출고상태 = "출고예정",
            수정일시 = new(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc)
        }],
        TotalCount = 26, Page = 1, PageSize = 25
    };

    private static 판매채널주문목록응답 DraftResponse(string search = DraftSearch) => new()
    {
        Items = [new()
        {
            OrderId = 41, 주문참조번호 = "SMARTSTORE-B-41", 채널종류 = CommerceChannelKeys.SmartStore,
            채널주문번호 = "VIRTUAL-B-41", 국내외구분 = CommerceChannelOrderSyncScopes.Domestic,
            대표상품명 = $"{search} 출고 후보", 총수량 = 8, 출고라인수 = 1, 출고창고수 = 1,
            출고창고표시 = "가상 출고센터", 출고상태 = "출고준비중",
            수정일시 = new(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc)
        }],
        TotalCount = 1, Page = 0, PageSize = 25
    };

    private sealed class Context : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        internal readonly OrderService Orders = new();
        internal readonly 판매채널주문PageViewModel Model;
        internal readonly HtmlRenderer Renderer;
        internal readonly CapturedControls Controls;
        private Func<string> rawHtml = () => string.Empty;

        internal Context()
        {
            Model = new(new(new AccessService(), new User()), new(Orders), new(Orders));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton(Model);
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(new Navigation());
            services.AddSingleton<CapturedControls>();
            services.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturedControls>());
            provider = services.BuildServiceProvider();
            Controls = provider.GetRequiredService<CapturedControls>();
            Renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        internal Task StartAsync() => Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Renderer.RenderComponentAsync<RoleAppProviders>();
            var output = await Renderer.RenderComponentAsync<WorkspaceHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(WorkspaceHost.Search)] = AppliedSearch,
                [nameof(WorkspaceHost.Scope)] = CommerceChannelOrderSyncScopes.Overseas,
                [nameof(WorkspaceHost.Status)] = "출고예정",
                [nameof(WorkspaceHost.Page)] = 2
            }));
            rawHtml = output.ToHtmlString;
        });

        internal Task ChangeToDraftBAsync() => Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Assert.Single(Controls.TextFields, field => field.Label == "주문번호·상품·SKU").ValueChanged.InvokeAsync(DraftSearch);
            await Assert.Single(Controls.Selects, select => select.Label == "국내·해외").ValueChanged.InvokeAsync(CommerceChannelOrderSyncScopes.Domestic);
            await Assert.Single(Controls.Selects, select => select.Label == "출고 상태").ValueChanged.InvokeAsync("출고준비중");
        });

        internal Task<MudButton> ButtonAsync(string label) => Renderer.Dispatcher.InvokeAsync(async () =>
        {
            foreach (var button in Controls.Buttons.ToArray().Reverse())
            {
                if (button.ChildContent is null) continue;
                var output = await Renderer.RenderComponentAsync<FragmentHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(FragmentHost.ChildContent)] = button.ChildContent
                }));
                if (string.Equals(WebUtility.HtmlDecode(output.ToHtmlString()).Trim(), label, StringComparison.Ordinal)) return button;
            }
            throw new Xunit.Sdk.XunitException("실제 렌더의 버튼이 없습니다. " + label);
        });

        internal Task ClickAsync(MudButton button)
            => Renderer.Dispatcher.InvokeAsync(() => button.OnClick.InvokeAsync(new MouseEventArgs()));

        internal Task<SalesOrderNavigationContext> ReturnContextAsync(long orderId = 42) => Renderer.Dispatcher.InvokeAsync(() =>
        {
            // Read the actual rendered anchor, not DetailPath or the proposed internal snapshot property.
            var hrefs = Regex.Matches(rawHtml(), "<a\\b[^>]*\\bhref=\"([^\"]+)\"", RegexOptions.CultureInvariant)
                .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
                .Where(href => new Uri(new Uri("http://localhost/"), href).AbsolutePath == $"/shipper/sales/orders/{orderId}").ToArray();
            var href = Assert.Single(hrefs);
            var detail = SalesOrderNavigationContext.Parse(href);
            Assert.False(string.IsNullOrWhiteSpace(detail.From));
            var returnPath = detail.ResolveReturnPath();
            Assert.Equal("/shipper/sales/orders", new Uri(new Uri("http://localhost/"), returnPath).AbsolutePath);
            return SalesOrderNavigationContext.Parse(returnPath);
        });

        public async ValueTask DisposeAsync()
        {
            Orders.ReleaseAll();
            await Renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class WorkspaceHost : ComponentBase
    {
        public WorkspaceHost() { }
        [Parameter] public string? Search { get; set; }
        [Parameter] public string? Scope { get; set; }
        [Parameter] public string? Status { get; set; }
        [Parameter] public int Page { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ShipperSalesOrderWorkspace>(0);
            builder.AddAttribute(1, nameof(ShipperSalesOrderWorkspace.Mode), ShipperSalesOrderWorkspaceMode.List);
            builder.AddAttribute(2, nameof(ShipperSalesOrderWorkspace.InitialSearch), Search);
            builder.AddAttribute(3, nameof(ShipperSalesOrderWorkspace.InitialSyncScope), Scope);
            builder.AddAttribute(4, nameof(ShipperSalesOrderWorkspace.InitialStatus), Status);
            builder.AddAttribute(5, nameof(ShipperSalesOrderWorkspace.InitialPage), Page);
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
        internal List<MudTextField<string>> TextFields { get; } = [];
        internal List<MudSelect<string>> Selects { get; } = [];
        internal List<MudButton> Buttons { get; } = [];
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, type);
            if (component is MudTextField<string> field) TextFields.Add(field);
            if (component is MudSelect<string> select) Selects.Add(select);
            if (component is MudButton button) Buttons.Add(button);
            return component;
        }
    }

    private sealed class OrderService : I판매채널주문읽기Service
    {
        internal List<판매채널주문목록조회요청> Requests { get; } = [];
        internal List<long> DetailRequests { get; } = [];
        private readonly List<PendingRead> pending = [];
        private PendingRead? next;
        internal PendingRead DeferNext()
        {
            var value = next = new PendingRead();
            pending.Add(value);
            return value;
        }
        public async Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (next is not { } value) return AppliedResponse();
            next = null;
            value.Started.TrySetResult();
            return await value.Response.Task.WaitAsync(cancellationToken);
        }
        public Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(orderId);
            return Task.FromResult<판매채널주문상세응답?>(null);
        }
        internal void ReleaseAll() { foreach (var value in pending) value.Release(); }
    }

    private sealed class PendingRead
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<판매채널주문목록응답> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Release(판매채널주문목록응답? response = null) => Response.TrySetResult(response ?? AppliedResponse());
    }

    private sealed class AccessService : I판매채널페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 { get; } = new("synthetic-seller", "가상 판매자", ["판매자"]);
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/shipper/sales/orders");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
