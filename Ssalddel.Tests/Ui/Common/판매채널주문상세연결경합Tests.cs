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

public sealed class 판매채널주문상세연결경합Tests
{
    private const long InitialOrder = 4101;
    private const long OldOrder = 4201;
    private const long CurrentOrder = 4301;
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 상세주소를B에서C로바꾸면_늦은B성공이나실패가_C화면을바꾸지않는다(bool oldFails)
    {
        await using var context = new Context();
        await context.StartAsync(InitialOrder);
        var originalComponent = await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace);
        var old = context.Orders.Block(OldOrder);
        var changingToOld = context.ChangeOrderAsync(OldOrder);
        await old.Started.Task.WaitAsync(SignalTimeout);

        string currentBeforeOldCompletes;
        try
        {
            await context.ChangeOrderAsync(CurrentOrder).WaitAsync(SignalTimeout);
            currentBeforeOldCompletes = await context.HtmlAsync();
        }
        finally
        {
            CompleteOld(old, oldFails);
            await changingToOld.WaitAsync(SignalTimeout);
        }

        Assert.Same(originalComponent, context.Workspace);
        Assert.Contains(CurrentOrder, context.Orders.DetailRequests);
        AssertCurrentOrder(context, currentBeforeOldCompletes);
        AssertCurrentOrder(context, await context.HtmlAsync());
        Assert.Equal(0, context.Orders.ListReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 최초상세초기화중주소가B에서C로바뀌어도_마지막C를조회하고표시한다(bool oldFails)
    {
        await using var context = new Context();
        var old = context.Orders.Block(OldOrder);
        var initializing = context.StartAsync(OldOrder);
        await old.Started.Task.WaitAsync(SignalTimeout);
        var originalComponent = await context.Renderer.Dispatcher.InvokeAsync(() => context.Workspace);

        try
        {
            await context.ChangeOrderAsync(CurrentOrder).WaitAsync(SignalTimeout);
        }
        finally
        {
            CompleteOld(old, oldFails);
            await initializing.WaitAsync(SignalTimeout);
        }

        Assert.Same(originalComponent, context.Workspace);
        Assert.Contains(CurrentOrder, context.Orders.DetailRequests);
        AssertCurrentOrder(context, await context.HtmlAsync());
        Assert.Equal(0, context.Orders.ListReads);
    }

    private static void CompleteOld(PendingRead pending, bool fails)
    {
        if (fails)
            pending.Response.TrySetException(new HttpRequestException("B-only-old-failure", null, HttpStatusCode.ServiceUnavailable));
        else
            pending.Response.TrySetResult(Order(OldOrder));
    }

    private static void AssertCurrentOrder(Context context, string html)
    {
        Assert.Equal(CurrentOrder, context.Workspace.OrderId);
        Assert.Equal(CurrentOrder, context.Model.상세.요청OrderId);
        var detail = Assert.IsType<판매채널주문상세응답>(context.Model.상세.상세);
        Assert.Equal(CurrentOrder, detail.주문.OrderId);
        Assert.Equal(18, detail.주문.총수량);
        Assert.Equal(new long[] { 4301, 4302, 4303 }, detail.출고라인목록.Select(line => line.Id));
        Assert.False(context.Model.상세.처리중);
        Assert.False(context.Model.상세.오류발생);
        Assert.Contains("VIRTUAL-SS-1107-C", html);
        Assert.Contains("VIRTUAL-C-SKU-4303", html);
        Assert.DoesNotContain("VIRTUAL-SS-1107-B", html);
        Assert.DoesNotContain("VIRTUAL-B-SKU", html);
        Assert.DoesNotContain("B-only-old-failure", html);
        Assert.DoesNotContain("주문 상세를 불러오지 못했습니다.", html);
        Assert.DoesNotContain("선택한 주문의 출고 후보 라인을 불러오고 있습니다.", html);
    }

    private sealed class Context : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private Func<string> renderHtml = () => string.Empty;
        internal readonly OrderService Orders = new();
        internal readonly 판매채널주문PageViewModel Model;
        internal readonly HtmlRenderer Renderer;
        internal ShipperSalesOrderWorkspace Workspace { get; private set; } = null!;

        internal Context()
        {
            Model = new(new(new AccessService(), new CurrentUser()), new(Orders), new(Orders, new FixedRecoveryContext()));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton(Model);
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager, TestNavigation>();
            provider = services.BuildServiceProvider();
            Renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        internal Task<string> StartAsync(long orderId)
            => Renderer.Dispatcher.InvokeAsync(async () =>
            {
                await Renderer.RenderComponentAsync<RoleAppProviders>();
                var root = Renderer.BeginRenderingComponent<WorkspaceHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(WorkspaceHost.OrderId)] = orderId,
                    [nameof(WorkspaceHost.Capture)] = (Action<ShipperSalesOrderWorkspace>)(workspace => Workspace = workspace)
                }));
                renderHtml = () => WebUtility.HtmlDecode(root.ToHtmlString());
                await root.QuiescenceTask;
                return renderHtml();
            });

        internal Task ChangeOrderAsync(long orderId)
            => Renderer.Dispatcher.InvokeAsync(() => Workspace.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShipperSalesOrderWorkspace.Mode)] = ShipperSalesOrderWorkspaceMode.Detail,
                [nameof(ShipperSalesOrderWorkspace.OrderId)] = orderId
            })));

        internal Task<string> HtmlAsync() => Renderer.Dispatcher.InvokeAsync(renderHtml);

        public async ValueTask DisposeAsync()
        {
            Orders.ReleasePending();
            await Renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    // The host captures the actual shared component; it contains no copied product rendering or API logic.
    private sealed class WorkspaceHost : ComponentBase
    {
        public WorkspaceHost() { }
        [Parameter] public long OrderId { get; set; }
        [Parameter] public Action<ShipperSalesOrderWorkspace>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ShipperSalesOrderWorkspace>(0);
            builder.AddAttribute(1, nameof(ShipperSalesOrderWorkspace.Mode), ShipperSalesOrderWorkspaceMode.Detail);
            builder.AddAttribute(2, nameof(ShipperSalesOrderWorkspace.OrderId), OrderId);
            builder.AddComponentReferenceCapture(3, component => Capture?.Invoke((ShipperSalesOrderWorkspace)component));
            builder.CloseComponent();
        }
    }

    private sealed class PendingRead
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<판매채널주문상세응답?> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class OrderService : I판매채널주문읽기Service
    {
        private readonly Dictionary<long, PendingRead> pending = [];
        internal readonly List<long> DetailRequests = [];
        internal int ListReads;
        internal PendingRead Block(long orderId) => pending[orderId] = new();
        internal void ReleasePending()
        {
            foreach (var read in pending.Values) read.Response.TrySetResult(null);
        }
        public Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            ListReads++;
            return Task.FromResult(new 판매채널주문목록응답());
        }
        public async Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(orderId);
            if (pending.TryGetValue(orderId, out var read))
            {
                read.Started.TrySetResult();
                // An already-started read deliberately ignores cancellation to exercise a genuinely late response.
                return await read.Response.Task;
            }
            return Order(orderId);
        }
    }

    private static 판매채널주문상세응답 Order(long orderId)
    {
        var key = orderId == OldOrder ? "B" : orderId == CurrentOrder ? "C" : "A";
        int[] quantities = orderId == CurrentOrder ? [12, 4, 2] : orderId == OldOrder ? [4, 6] : [1];
        return new()
        {
            주문 = new()
            {
                OrderId = orderId, 주문참조번호 = $"SmartStore:VIRTUAL-SS-1107-{key}", 채널종류 = CommerceChannelKeys.SmartStore,
                채널주문번호 = $"VIRTUAL-SS-1107-{key}", 국내외구분 = CommerceChannelOrderSyncScopes.Domestic,
                출고상태 = quantities.Length > 1 ? "복합 상태" : "출고예정", 출고라인수 = quantities.Length,
                총수량 = quantities.Sum(), 출고창고수 = quantities.Length > 1 ? 2 : 1, 출고창고표시 = "가상 창고",
                대표상품명 = $"가상 {key} 주문", 생성일시 = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), 수정일시 = new(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc)
            },
            출고라인목록 = quantities.Select((quantity, index) => new 판매채널주문출고라인응답
            {
                Id = orderId + index, 상품명 = $"가상 {key} 상품 · {index + 1}", SKU = $"VIRTUAL-{key}-SKU-{orderId + index}", 수량 = quantity,
                출고창고Id = index % 2 + 701, 출고창고명 = $"가상 창고 {index % 2 + 1}", 상태 = index == 0 ? "출고예정" : "출고준비중"
            }).ToArray()
        };
    }

    private sealed class AccessService : I판매채널페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class CurrentUser : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 { get; } = new("synthetic-seller", "가상 판매자", ["판매자"]);
    }
    private sealed class FixedRecoveryContext : IWorkflowRecoveryContext
    {
        private readonly WorkflowRecoverySnapshot current = new("synthetic-seller", 1, new string('a', 32));
        public WorkflowRecoverySnapshot Capture() => current;
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => current == snapshot;
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://fixture.invalid/", "http://fixture.invalid/shipper/sales/orders/4101");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("Rendering test does not navigate.");
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
