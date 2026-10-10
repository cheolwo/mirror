using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMemberMapLifecycleTests
{
    [Fact]
    public async Task 앱재개는_같은업무판본의지도를다시표시하고_종료후이벤트를해제한다()
    {
        await using var context = new Context(); await context.Start();
        Assert.Equal(1, context.Map.RenderCount);
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Map.Suspend());
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Map.Resume());
        Assert.Equal(2, context.Map.RenderCount); Assert.Equal(2, context.Map.Subscribers);
        await context.Renderer.DisposeAsync(); Assert.Equal(0, context.Map.Subscribers);
        context.Map.Resume(); Assert.Equal(2, context.Map.RenderCount);
    }
    [Fact]
    public async Task 지도연속예외는_자동반복하지않고_업무를보존하며_명시재시도를허용한다()
    {
        await using var context = new Context(); context.Map.Fail = true;
        await context.Start(); await context.Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(1, context.Map.RenderCount); Assert.NotNull(context.Model.Work);
        Assert.Equal("Assigned", context.Model.Work.StateCode); Assert.Equal("work", context.Model.Work.RequestId);
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Renderer.ClickRetry());
        Assert.Equal(2, context.Map.RenderCount); Assert.Equal(0, context.Fixture.PickupCommands.Count);
    }
    [Fact]
    public async Task 중지중늦은렌더는_복귀지도를확정하지않는다()
    {
        await using var context = new Context(); context.Map.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = context.Start(); await context.Map.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Map.Suspend());
        context.Map.Pending.SetResult(new("ready")); await start;
        await context.Map.Hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(1, context.Map.RenderCount); Assert.True(context.Map.HideCount > 0);
        context.Map.Pending = null;
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Map.Resume());
        Assert.Equal(2, context.Map.RenderCount);
    }
    [Theory]
    [InlineData("unavailable")] [InlineData("unconfigured")] [InlineData("blocked-origin")]
    [InlineData("auth-failed")] [InlineData("timeout")]
    public async Task 공급자미준비상태도_자동반복없이_명시재시도를제공한다(string code)
    {
        await using var context = new Context(); context.Map.StatusCode = code;
        await context.Start(); await context.Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(1, context.Map.RenderCount);
        await context.Renderer.Dispatcher.InvokeAsync(() => context.Renderer.ClickRetry());
        Assert.Equal(2, context.Map.RenderCount); Assert.NotNull(context.Model.Work);
        Assert.Empty(context.Fixture.PickupCommands);
    }
    private sealed class Context : IAsyncDisposable
    {
        internal readonly NeighborhoodMemberDeliveryUiTests.Fixture Fixture = new();
        internal readonly Map Map = new();
        internal readonly Ssalddel.Ui.Common.Areas.App.ViewModels.NeighborhoodMemberWorkViewModel Model;
        internal readonly MemberRenderer Renderer;
        private readonly ServiceProvider provider;
        internal Context()
        {
            Fixture.CurrentWork.StateCode = "Assigned"; Model = Fixture.Work();
            var services = new ServiceCollection(); services.AddLogging(); services.AddCommerceUiFixture();
            services.AddSingleton<NavigationManager>(new Nav()); services.AddSingleton(Model); services.AddSingleton<INeighborhoodMapHost>(Map);
            provider = services.BuildServiceProvider(); Renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }
        internal Task Start() => Renderer.Dispatcher.InvokeAsync(Renderer.Start);
        public async ValueTask DisposeAsync() { await Renderer.DisposeAsync(); await provider.DisposeAsync(); }
    }
    private sealed class Map : INeighborhoodMapHost, INeighborhoodMapInteractionHost
    {
        internal int RenderCount, HideCount; internal bool Fail; internal string StatusCode = "ready";
        internal TaskCompletionSource<NeighborhoodMapHostStatus>? Pending;
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Hidden = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<NeighborhoodMapViewport>? ViewportChanged { add { } remove { } }
        public event Action? Suspended; public event Action? Invalidated;
        internal int Subscribers => (Suspended?.GetInvocationList().Length ?? 0) + (Invalidated?.GetInvocationList().Length ?? 0);
        internal void Suspend() => Suspended?.Invoke(); internal void Resume() => Invalidated?.Invoke();
        public Task HideAsync(CancellationToken ct = default) { HideCount++; Hidden.TrySetResult(); return Task.CompletedTask; }
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken ct = default)
        { RenderCount++; Started.TrySetResult(); return Fail ? Task.FromException<NeighborhoodMapHostStatus>(new InvalidOperationException("map unavailable")) : Pending?.Task ?? Task.FromResult(new NeighborhoodMapHostStatus(StatusCode)); }
    }
    private sealed class Nav : NavigationManager { internal Nav() => Initialize("https://example.test/", "https://example.test/work"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
#pragma warning disable BL0006
    private sealed class MemberRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault(); private int root;
        protected override void HandleException(Exception exception) => throw exception;
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        internal Task Start() { root = AssignRootComponentId(InstantiateComponent(typeof(NeighborhoodMemberWork))); return RenderRootComponentAsync(root,
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NeighborhoodMemberWork.RequestId)] = "work" })); }
        internal Task ClickRetry()
        {
            var frames = GetCurrentRenderTreeFrames(root);
            for (var i = 0; i < frames.Count; i++)
            {
                if (frames.Array[i].FrameType != RenderTreeFrameType.Element || frames.Array[i].ElementName != "button") continue;
                var content = frames.Array.Skip(i + 1).Take(frames.Array[i].ElementSubtreeLength - 1).ToArray();
                if (!content.Any(frame => frame.FrameType == RenderTreeFrameType.Text && frame.TextContent == "지도 다시 표시"
                    || frame.FrameType == RenderTreeFrameType.Markup && frame.MarkupContent == "지도 다시 표시")) continue;
                var click = content.Single(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick");
                return DispatchEventAsync(click.AttributeEventHandlerId, null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            }
            throw new InvalidOperationException("map retry control absent");
        }
    }
#pragma warning restore BL0006
}
