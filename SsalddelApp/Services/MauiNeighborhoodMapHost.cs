using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Services;

public sealed class MauiNeighborhoodMapHost : INeighborhoodMapHost, INeighborhoodMapInteractionHost, IAsyncDisposable
{
    private readonly IJSRuntime js;
    private readonly NeighborhoodNativeMapBridge bridge;
    private readonly object _sync = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<MauiNeighborhoodMapHost>? _reference;
    private string? _elementId;
    private CancellationTokenSource? _renderCancellation;
    private long _generation;
    private bool _disposed, _suspended;
    private int _hiding;
    private TaskCompletionSource<bool>? _hideFinished;
    private Window? _window;
    private bool? _boundsVisible;
    public event Action? Invalidated;
    public event Action? Suspended;
    public event Action<NeighborhoodMapViewport>? ViewportChanged;
    public MauiNeighborhoodMapHost(IJSRuntime js, NeighborhoodNativeMapBridge bridge)
    {
        this.js = js; this.bridge = bridge;
        bridge.ViewportChanged += OnViewportChanged;
    }
    private void OnViewportChanged(NeighborhoodMapViewport viewport)
    {
        lock (_sync) if (_disposed || _suspended || _elementId is null) return;
        if (double.IsFinite(viewport.Latitude) && viewport.Latitude is >= -90 and <= 90
            && double.IsFinite(viewport.Longitude) && viewport.Longitude is >= -180 and <= 180
            && double.IsFinite(viewport.Zoom) && viewport.Zoom is >= 3 and <= 20)
            ViewportChanged?.Invoke(viewport);
    }

    public async Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state,
        Func<string, Task> markerSelected, CancellationToken cancellationToken = default)
    {
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(WatchWindow);
        var render = await BeginRenderAsync(cancellationToken);
        if (render is null) return NeighborhoodMapHostStatus.Unavailable;
        var (generation, lifetime) = render.Value;
        var token = lifetime.Token;
        try
        {
            IJSObjectReference? module;
            lock (_sync) module = _module;
            if (module is null)
            {
                var imported = await js.InvokeAsync<IJSObjectReference>("import", token, "./js/native-neighborhood-map.js");
                lock (_sync)
                {
                    if (Current(generation)) module = _module ??= imported;
                }
                if (!ReferenceEquals(imported, module)) await DisposeModuleAsync(imported);
                if (module is null) return NeighborhoodMapHostStatus.Unavailable;
            }
            DotNetObjectReference<MauiNeighborhoodMapHost> reference;
            lock (_sync)
            {
                if (!Current(generation)) return NeighborhoodMapHostStatus.Unavailable;
                reference = _reference ??= DotNetObjectReference.Create(this);
                _elementId = elementId;
            }
            var bounds = await module.InvokeAsync<NeighborhoodMapBounds>("watch", token, elementId, reference);
            if (!IsCurrent(generation)) return NeighborhoodMapHostStatus.Unavailable;
            lock (_sync) _boundsVisible = bounds.Visible;
            if (!bounds.Visible)
            {
                // 입력/대화상자가 닫힌 뒤 다시 그릴 수 있도록 DOM 관찰은 유지합니다.
                await bridge.MoveAsync(bounds);
                return NeighborhoodMapHostStatus.Loading;
            }
            var status = await bridge.RenderAsync(bounds, state, markerSelected, token);
            return IsCurrent(generation) ? status : NeighborhoodMapHostStatus.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NeighborhoodMapHostStatus.Unavailable;
        }
        catch (OperationCanceledException)
        {
            await HideCoreAsync(generation, CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            // 지도 또는 종료된 WebView 오류를 사용자 정보와 함께 전파하지 않습니다.
            await HideCoreAsync(generation, CancellationToken.None);
            return NeighborhoodMapHostStatus.Unavailable;
        }
        finally
        {
            lock (_sync)
                if (ReferenceEquals(_renderCancellation, lifetime)) _renderCancellation = null;
            lifetime.Dispose();
        }
#else
        await bridge.HideAsync();
        return new NeighborhoodMapHostStatus("unsupported", "이 기기에서는 목록으로 확인해 주세요. 지도는 Android 앱과 웹에서 지원합니다.");
#endif
    }

    [JSInvokable]
    public Task UpdateBounds(NeighborhoodMapBounds bounds)
    {
        long generation;
        bool appeared;
        lock (_sync)
        {
            if (_disposed || _suspended || _elementId is null || _hiding > 0) return Task.CompletedTask;
            generation = _generation;
            appeared = bounds.Visible && _boundsVisible != true;
            _boundsVisible = bounds.Visible;
        }
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (!IsCurrent(generation)) return;
            await bridge.MoveAsync(bounds);
            if (appeared && IsCurrent(generation)) Invalidated?.Invoke();
        });
    }

    public Task HideAsync(CancellationToken cancellationToken = default) => HideCoreAsync(null, cancellationToken);

    private async Task<(long Generation, CancellationTokenSource Lifetime)?> BeginRenderAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task? wait;
            CancellationTokenSource? previous = null;
            (long, CancellationTokenSource)? render = null;
            lock (_sync)
            {
                if (_disposed || _suspended) return null;
                wait = _hiding > 0 ? _hideFinished!.Task : null;
                if (wait is null)
                {
                    previous = _renderCancellation;
                    var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _renderCancellation = lifetime;
                    render = (++_generation, lifetime);
                }
            }
            if (render is not null) { Cancel(previous); return render; }
            await wait!.WaitAsync(cancellationToken);
        }
    }

    private bool Current(long generation) => !_disposed && !_suspended && _hiding == 0 && generation == _generation;
    private bool IsCurrent(long generation) { lock (_sync) return Current(generation); }

    private async Task HideCoreAsync(long? expectedGeneration, CancellationToken cancellationToken)
    {
        IJSObjectReference? module;
        string? elementId;
        CancellationTokenSource? render;
        lock (_sync)
        {
            if (expectedGeneration is { } expected && expected != _generation) return;
            ++_generation;
            if (_hiding++ == 0) _hideFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            module = _module; elementId = _elementId; _elementId = null;
            _boundsVisible = null;
            render = _renderCancellation; _renderCancellation = null;
        }
        Cancel(render);
        try
        {
            // 네이티브 지도를 먼저 제거합니다. JS가 이미 닫혔어도 개인 좌표는 남기지 않습니다.
            await bridge.HideAsync();
            if (module is not null && elementId is not null)
            {
                try { await module.InvokeVoidAsync("unwatch", cancellationToken, elementId); }
                catch (JSException) { }
                catch (InvalidOperationException) { }
            }
        }
        finally
        {
            lock (_sync)
                if (--_hiding == 0) _hideFinished!.TrySetResult(true);
        }
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); } catch (ObjectDisposedException) { }
    }

    private static async Task DisposeModuleAsync(IJSObjectReference module)
    {
        try { await module.DisposeAsync(); } catch (JSException) { } catch (InvalidOperationException) { }
    }

    private void WatchWindow()
    {
        lock (_sync) if (_disposed) return;
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (ReferenceEquals(window, _window)) return;
        UnwatchWindow(); _window = window;
        if (_window is null) return;
        _window.Stopped += OnWindowStopped;
        _window.Resumed += OnWindowResumed;
    }
    private void UnwatchWindow()
    {
        if (_window is null) return;
        _window.Stopped -= OnWindowStopped;
        _window.Resumed -= OnWindowResumed;
        _window = null;
    }
    private async void OnWindowStopped(object? sender, EventArgs args)
    {
        lock (_sync) { if (_disposed) return; _suspended = true; }
        Suspended?.Invoke();
        try { await HideCoreAsync(null, CancellationToken.None); } catch (Exception) { }
    }
    private void OnWindowResumed(object? sender, EventArgs args)
    {
        // 지도 객체를 새로 그리도록 알립니다. 개인 경로는 현재 권한과 위치를 재조회한 뒤 표시합니다.
        lock (_sync) { if (_disposed) return; _suspended = false; }
        MainThread.BeginInvokeOnMainThread(() =>
        {
            lock (_sync) if (_disposed || _suspended) return;
            Invalidated?.Invoke();
        });
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        bridge.ViewportChanged -= OnViewportChanged;
        try { await HideAsync(); }
        finally
        {
            try { await MainThread.InvokeOnMainThreadAsync(UnwatchWindow); } catch (Exception) { }
            _reference?.Dispose(); _reference = null;
            IJSObjectReference? module;
            lock (_sync) { module = _module; _module = null; }
            if (module is not null) await DisposeModuleAsync(module);
        }
    }
}
