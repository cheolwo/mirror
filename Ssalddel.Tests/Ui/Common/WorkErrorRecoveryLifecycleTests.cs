using System.Reflection;
using System.Threading.Channels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components.Recovery;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkErrorRecoveryLifecycleTests
{
    [Fact]
    public async Task 닫은안내는_반복조회나_Busy빈문구뒤_같은분류실패로재개방하지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500 trace1");
        var show = await f.Dialogs.Next(); var dialog = ReferenceProxy.Create();
        show.SetResult(dialog.Reference); await dialog.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Complete(null); await f.Drain();
        await f.Render("A", null, busy: true);
        await f.Render("A", "HTTP500 trace2"); await f.Drain();
        Assert.Equal(1, f.Dialogs.ShowCount); Assert.Equal(0, f.Retries); Assert.Empty(f.Navigation.Visited);
    }

    [Fact]
    public async Task 명시재조회는_GET콜백한번만호출하고_실패가남으면_새안내를열수있다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500");
        var show = await f.Dialogs.Next(); var dialog = ReferenceProxy.Create();
        show.SetResult(dialog.Reference); await dialog.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Complete("retry");
        var again = await f.Dialogs.Next();
        Assert.Equal(1, f.Retries); Assert.Equal(2, f.Dialogs.ShowCount);
        var second = ReferenceProxy.Create(); again.SetResult(second.Reference);
        await second.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Stop(); Assert.True(second.Closed);
    }

    [Fact]
    public async Task 대상변경중늦게생성된이전창은_닫고_새대상창만유지한다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500"); var firstShow = await f.Dialogs.Next();
        await f.Render("B", "HTTP404");
        var old = ReferenceProxy.Create(); firstShow.SetResult(old.Reference);
        await old.ClosedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var newShow = await f.Dialogs.Next(); var current = ReferenceProxy.Create(); newShow.SetResult(current.Reference);
        await current.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(old.Closed); Assert.False(current.Closed);
        await f.Render("B", null);
        await current.ClosedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(f.Navigation.Visited); Assert.Equal(0, f.Retries);
    }

    [Fact]
    public async Task 페이지폐기후늦은다이얼로그응답은_닫고_이동이나재조회를하지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500"); var firstShow = await f.Dialogs.Next();
        await f.Stop(); var late = ReferenceProxy.Create(); firstShow.SetResult(late.Reference);
        await late.ClosedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(f.Navigation.Visited); Assert.Equal(0, f.Retries);
    }

    [Fact]
    public async Task 공통조각의수동안내모드는_자동창을열지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500", autoOpen: false);
        await f.Drain(); Assert.Equal(0, f.Dialogs.ShowCount);
    }

    [Fact]
    public async Task 미확정맥락은_화면복귀를요청해도_조회대상을벗어나지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", null, error: new Api작업오류("CargoTransportNotFound", "RAW", 404,
            WorkflowContext: new("DomesticCargoTransportOS", "cargo.pickup", 업무처리결과Codes.미확정)));
        var show = await f.Dialogs.Next();
        Assert.False((bool)f.Dialogs.LastParameters![nameof(업무오류복구Dialog.CanReturn)]!);
        var dialog = ReferenceProxy.Create(); show.SetResult(dialog.Reference);
        await dialog.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Complete("return"); await f.Drain();
        Assert.Empty(f.Navigation.Visited); Assert.Equal(0, f.Retries);
    }

    [Fact]
    public async Task 사진필요사유는_재조회요청을주입해도_콜백을실행하지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", null, error: new Api작업오류("CargoPickupPhotoRequired", "RAW",
            WorkflowContext: new("DomesticCargoTransportOS", "cargo.pickup", 업무처리결과Codes.미적용)));
        var show = await f.Dialogs.Next();
        Assert.False((bool)f.Dialogs.LastParameters![nameof(업무오류복구Dialog.CanRetry)]!);
        var dialog = ReferenceProxy.Create(); show.SetResult(dialog.Reference);
        await dialog.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Complete("retry"); await f.Drain();
        Assert.Equal(0, f.Retries); Assert.Empty(f.Navigation.Visited);
    }

    [Fact]
    public async Task 이전창종료가_새창생성대기를해제해_수동안내를중복생성하지않는다()
    {
        await using var f = new Fixture();
        await f.Render("A", "HTTP500"); var firstShow = await f.Dialogs.Next();
        var old = ReferenceProxy.Create(); old.DeferCloseResult = true; firstShow.SetResult(old.Reference);
        await old.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Render("B", "HTTP404");
        var secondShow = await f.Dialogs.Next();
        var repainted = f.NextDisplay(); old.Complete(null);
        await repainted.WaitAsync(TimeSpan.FromSeconds(5));
        await f.ClickReopen().WaitAsync(TimeSpan.FromSeconds(5)); await f.Drain();
        Assert.Equal(2, f.Dialogs.ShowCount);
        var current = ReferenceProxy.Create(); secondShow.SetResult(current.Reference);
        await current.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(current.Closed);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly RecoveryRenderer _renderer;
        private bool _stopped;
        public DialogProxy Dialogs { get; }
        public TestNavigation Navigation { get; } = new();
        public int Retries { get; private set; }
        public Fixture()
        {
            var dialogService = DispatchProxy.Create<IDialogService, DialogProxy>(); Dialogs = (DialogProxy)dialogService;
            var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
            services.AddSingleton<IDialogService>(dialogService);
            services.AddSingleton<NavigationManager>(Navigation); services.AddSingleton<IJSRuntime, NoopJs>();
            _services = services.BuildServiceProvider();
            _renderer = new(_services, _services.GetRequiredService<ILoggerFactory>());
        }
        public Task Render(string context, string? message, bool busy = false, bool autoOpen = true, Api작업오류? error = null)
            => _renderer.Dispatcher.InvokeAsync(() => _renderer.Render(new Dictionary<string, object?>
            {
                [nameof(업무오류복구Panel.ContextKey)] = context,
                [nameof(업무오류복구Panel.Message)] = message,
                [nameof(업무오류복구Panel.Error)] = error,
                [nameof(업무오류복구Panel.Busy)] = busy,
                [nameof(업무오류복구Panel.AutoOpen)] = autoOpen,
                [nameof(업무오류복구Panel.ReturnHref)] = "/work",
                [nameof(업무오류복구Panel.RetryRequested)] = EventCallback.Factory.Create(this, () => Retries++)
            }));
        public async Task Drain()
        {
            for (var i = 0; i < 4; i++) await _renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
            Assert.Empty(_renderer.Errors);
        }
        public Task NextDisplay() => _renderer.NextDisplay();
        public Task ClickReopen() => _renderer.Dispatcher.InvokeAsync(_renderer.ClickReopen);
        public async Task Stop()
        {
            if (_stopped) return; _stopped = true;
            await _renderer.Dispatcher.InvokeAsync(_renderer.Remove);
        }
        public async ValueTask DisposeAsync()
        {
            await Stop(); await Drain(); await _renderer.DisposeAsync(); await _services.DisposeAsync();
        }
    }

    public class DialogProxy : DispatchProxy
    {
        private readonly Channel<TaskCompletionSource<IDialogReference>> _shows = Channel.CreateUnbounded<TaskCompletionSource<IDialogReference>>();
        public int ShowCount { get; private set; }
        public DialogParameters? LastParameters { get; private set; }
        public async Task<TaskCompletionSource<IDialogReference>> Next()
            => await _shows.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "ShowAsync")
            {
                LastParameters = args?.OfType<DialogParameters>().SingleOrDefault();
                ShowCount++; var show = new TaskCompletionSource<IDialogReference>(TaskCreationOptions.RunContinuationsAsynchronously);
                _shows.Writer.TryWrite(show); return show.Task;
            }
            if (method?.Name.StartsWith("add_", StringComparison.Ordinal) == true || method?.Name.StartsWith("remove_", StringComparison.Ordinal) == true) return null;
            throw new InvalidOperationException("Unexpected dialog operation: " + method?.Name);
        }
    }

    public class ReferenceProxy : DispatchProxy
    {
        private readonly TaskCompletionSource<DialogResult?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ClosedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDialogReference Reference { get; private set; } = default!;
        public bool Closed { get; private set; }
        public bool DeferCloseResult { get; set; }
        public static ReferenceProxy Create()
        {
            var reference = DispatchProxy.Create<IDialogReference, ReferenceProxy>();
            var proxy = (ReferenceProxy)reference; proxy.Reference = reference; return proxy;
        }
        public void Complete(string? action)
        {
            Closed = true; ClosedSignal.TrySetResult();
            _result.TrySetResult(action is null ? DialogResult.Cancel() : DialogResult.Ok(action));
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "get_Result") { Observed.TrySetResult(); return _result.Task; }
            if (method?.Name == "Close")
            {
                Closed = true; ClosedSignal.TrySetResult();
                if (!DeferCloseResult) Complete(null);
                return null;
            }
            throw new InvalidOperationException("Unexpected dialog reference operation: " + method?.Name);
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public List<string> Visited { get; } = [];
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/work");
        protected override void NavigateToCore(string uri, bool forceLoad) => Visited.Add(uri);
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
    }

#pragma warning disable BL0006 // Test actual component lifecycle without an external test UI package.
    private sealed class RecoveryRenderer(IServiceProvider services, ILoggerFactory logging) : Renderer(services, logging)
    {
        private int? _id;
        private TaskCompletionSource? _nextDisplay;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<Exception> Errors { get; } = [];
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            _nextDisplay?.TrySetResult(); _nextDisplay = null; return Task.CompletedTask;
        }
        public Task NextDisplay()
        {
            _nextDisplay = new(TaskCreationOptions.RunContinuationsAsynchronously); return _nextDisplay.Task;
        }
        public Task ClickReopen()
        {
            var frames = GetCurrentRenderTreeFrames(_id!.Value);
            var button = frames.Array.Take(frames.Count)
                .Where(frame => frame.FrameType == RenderTreeFrameType.Component)
                .Select(frame => frame.Component).OfType<MudButton>().Single();
            return button.OnClick.InvokeAsync(new MouseEventArgs());
        }
        public Task Render(Dictionary<string, object?> parameters)
        {
            _id ??= AssignRootComponentId(InstantiateComponent(typeof(업무오류복구Panel)));
            return RenderRootComponentAsync(_id.Value, ParameterView.FromDictionary(parameters));
        }
        public void Remove() { if (_id.HasValue) RemoveRootComponent(_id.Value); _id = null; }
    }
#pragma warning restore BL0006
}
