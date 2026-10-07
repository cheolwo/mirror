using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

public sealed class NeighborhoodGoogleMapHost(IJSRuntime js, GoogleMapsBrowserRuntimeClient runtime) : INeighborhoodMapHost, INeighborhoodMapInteractionHost, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<NeighborhoodGoogleMapHost>? _reference;
    private Func<string, Task>? _selected;
    private string? _elementId;
    private long _epoch, _lifecycle;
    private bool _disposed, _suspended;
    public event Action? Invalidated;
    public event Action? Suspended;
    public event Action<NeighborhoodMapViewport>? ViewportChanged;
    // 공급자 허용은 렌더러와 분리합니다. 경로를 저장하거나 캐시하지 않습니다.
    private static readonly string[] AllowedRouteSources = ["NaverCloudDirections", "NaverDirections5", "cargo-straight-candidate"];
    public async Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state,
        Func<string, Task> markerSelected, CancellationToken cancellationToken = default)
    {
        if (_disposed || _suspended) return NeighborhoodMapHostStatus.Unavailable;
        var epoch = ++_epoch;
        _elementId = elementId; _selected = markerSelected;
        if (_module is null)
        {
            var imported = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/neighborhood-life-google-map.js");
            if (_disposed || epoch != _epoch)
            {
                await imported.DisposeAsync();
                return NeighborhoodMapHostStatus.Unavailable;
            }
            _module ??= imported;
            if (!ReferenceEquals(_module, imported)) await imported.DisposeAsync();
        }
        var settings = await runtime.TryGetAsync(cancellationToken);
        if (_disposed || epoch != _epoch) return NeighborhoodMapHostStatus.Unavailable;
        _reference ??= DotNetObjectReference.Create(this);
        var code = await _module.InvokeAsync<string>("render", cancellationToken, elementId, state, _reference, settings, AllowedRouteSources, epoch, _lifecycle);
        if (_disposed || epoch != _epoch) return NeighborhoodMapHostStatus.Unavailable;
        return code switch
        {
            "ready" => new("ready"),
            "unconfigured" => new(code, "지도 연결 설정을 확인 중입니다. 아래 목록을 이용해 주세요."),
            "blocked-origin" => new(code, "이 접속 환경에서는 지도를 사용할 수 없습니다. 아래 목록을 이용해 주세요."),
            "auth-failed" => new(code, "지도 연결 인증을 확인하지 못했습니다. 아래 목록을 이용해 주세요."),
            "timeout" => new(code, "지도 응답이 늦어지고 있습니다. 다시 시도하거나 목록을 이용해 주세요."),
            _ => NeighborhoodMapHostStatus.Unavailable
        };
    }
    [JSInvokable] public Task MarkerSelected(string markerId) => !_disposed && !_suspended && _selected is { } selected ? selected(markerId) : Task.CompletedTask;
    [JSInvokable] public Task MapMarkerSelected(string markerId, long epoch) => epoch == _epoch ? MarkerSelected(markerId) : Task.CompletedTask;
    [JSInvokable]
    public Task CameraChanged(double latitude, double longitude, double zoom, long epoch)
    {
        if (!_disposed && !_suspended && epoch == _epoch && _elementId is not null
            && double.IsFinite(latitude) && latitude is >= -90 and <= 90
            && double.IsFinite(longitude) && longitude is >= -180 and <= 180
            && double.IsFinite(zoom) && zoom is >= 3 and <= 20)
            ViewportChanged?.Invoke(new(latitude, longitude, zoom));
        return Task.CompletedTask;
    }
    [JSInvokable]
    public Task MapSuspended(long lifecycle)
    {
        if (_disposed || lifecycle != _lifecycle || _elementId is null) return Task.CompletedTask;
        _suspended = true; ++_epoch;
        Suspended?.Invoke();
        return Task.CompletedTask;
    }
    [JSInvokable]
    public Task MapInvalidated(long lifecycle)
    {
        if (_disposed || lifecycle != _lifecycle || _elementId is null) return Task.CompletedTask;
        _suspended = false;
        Invalidated?.Invoke();
        return Task.CompletedTask;
    }
    public async Task HideAsync(CancellationToken cancellationToken = default)
    {
        ++_epoch; ++_lifecycle; _selected = null; _suspended = false;
        var elementId = _elementId; _elementId = null;
        if (_module is not null && elementId is not null) await _module.InvokeVoidAsync("hide", cancellationToken, elementId, _epoch);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await HideAsync(); } catch (JSDisconnectedException) { } catch (ObjectDisposedException) { }
        _reference?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }
}
