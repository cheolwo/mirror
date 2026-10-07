namespace SsalddelApp;

public partial class MainPage : ContentPage
{
    private readonly Services.NeighborhoodNativeMapBridge _bridge;
    private Controls.NeighborhoodNativeMapView? _mapView;
    private Func<string, Task>? _selected;
    private long _generation;
    private bool _acceptMaps = true;
    private Window? _window;
    internal event Action<Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapViewport>? MapViewportChanged;
    public MainPage(Services.NeighborhoodNativeMapBridge bridge)
	{
        _bridge = bridge;
		InitializeComponent();
        _bridge.Attach(this);
	}
    internal async Task<Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus> ShowMapAsync(
        Services.NeighborhoodMapBounds bounds, Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapRenderState state,
        Func<string, Task> selected, CancellationToken cancellationToken)
    {
#if ANDROID
        cancellationToken.ThrowIfCancellationRequested();
        if (!_acceptMaps) return Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus.Unavailable;
        if (!GoogleMapConfigured()) return new("unconfigured", "지도 연결 설정이 필요합니다. 목록으로 확인해 주세요.");
        var generation = Interlocked.Increment(ref _generation);
        Controls.NeighborhoodNativeMapView? view = null;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || !Current(generation)) return;
                _selected = selected;
                if (_mapView is null)
                {
                    _mapView = new Controls.NeighborhoodNativeMapView { InputTransparent = false };
                    _mapView.MarkerSelected += OnMarkerSelected;
                    _mapView.ViewportChanged += OnViewportChanged;
                    mapOverlay.Children.Add(_mapView);
                }
                view = _mapView;
                view.State = state;
                MoveMap(bounds);
            });
            if (view is null || !Current(generation)) return Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus.Unavailable;
            var loaded = await view.TilesLoaded.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (loaded && Current(generation) && !cancellationToken.IsCancellationRequested) return new("ready");
            await HideGenerationAsync(generation);
            return Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus.Unavailable;
        }
        catch (TimeoutException)
        {
            await HideGenerationAsync(generation);
            return new("failed", "지도 연결을 확인하지 못했습니다. 다시 시도하거나 목록으로 확인해 주세요.");
        }
        catch (OperationCanceledException)
        {
            await HideGenerationAsync(generation);
            throw;
        }
        catch (Exception)
        {
            await HideGenerationAsync(generation);
            return new("failed", "지도를 표시하지 못했습니다. 목록으로 확인하거나 다시 시도해 주세요.");
        }
#else
        await Task.CompletedTask;
        return Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus.Unavailable;
#endif
    }
    internal void MoveMap(Services.NeighborhoodMapBounds bounds)
    {
        if (!_acceptMaps || _mapView is null) return;
        _mapView.IsVisible = bounds.Visible;
        if (!bounds.Visible || bounds.ViewportWidth <= 0) return;
        var ratio = blazorWebView.Width / bounds.ViewportWidth;
        AbsoluteLayout.SetLayoutBounds(_mapView, new Rect(bounds.X * ratio, bounds.Y * ratio, bounds.Width * ratio, bounds.Height * ratio));
    }
    internal void HideMap()
    {
        Interlocked.Increment(ref _generation); _selected = null;
        var view = _mapView; _mapView = null;
        if (view is null) return;
        view.MarkerSelected -= OnMarkerSelected;
        view.ViewportChanged -= OnViewportChanged;
        view.CompleteTiles(false);
        // 이미 폐기된 SDK의 해제 오류가 다른 정리나 페이지 이탈을 막지 않도록 각각 처리합니다.
        ReleaseMap(() => view.IsVisible = false);
        ReleaseMap(() => mapOverlay.Children.Remove(view));
        ReleaseMap(() => view.State = null);
        ReleaseMap(() => view.Handler?.DisconnectHandler());
    }
    private static void ReleaseMap(Action release) { try { release(); } catch (Exception) { } }
    private bool Current(long generation) => _acceptMaps && generation == Interlocked.Read(ref _generation);
    private async Task HideGenerationAsync(long generation)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (generation == Interlocked.Read(ref _generation)) HideMap();
            });
        }
        catch (Exception) { }
    }
    private async void OnMarkerSelected(object? sender, string id)
    {
        var generation = Interlocked.Read(ref _generation);
        if (!Current(generation) || !ReferenceEquals(sender, _mapView) || _selected is not { } selected) return;
        try { await selected(id); }
        catch (OperationCanceledException) { }
        catch (Exception) { await HideGenerationAsync(generation); }
    }
    private void OnViewportChanged(object? sender, Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapViewport viewport)
    {
        if (_acceptMaps && ReferenceEquals(sender, _mapView)) MapViewportChanged?.Invoke(viewport);
    }
    protected override void OnAppearing()
    {
        base.OnAppearing(); _acceptMaps = true; _bridge.Attach(this);
        UnwatchWindow(); _window = Window;
        if (_window is null) return;
        _window.Stopped += OnWindowStopped;
        _window.Resumed += OnWindowResumed;
    }
    protected override void OnDisappearing()
    {
        _acceptMaps = false; HideMap(); _bridge.Detach(this); UnwatchWindow(); base.OnDisappearing();
    }
    private void OnWindowStopped(object? sender, EventArgs args)
    {
        _acceptMaps = false; HideMap(); _bridge.Detach(this);
    }
    private void OnWindowResumed(object? sender, EventArgs args)
    {
        _acceptMaps = true; _bridge.Attach(this);
    }
    private void UnwatchWindow()
    {
        if (_window is null) return;
        _window.Stopped -= OnWindowStopped;
        _window.Resumed -= OnWindowResumed;
        _window = null;
    }
#if ANDROID
    private static bool GoogleMapConfigured()
    {
        var context = Android.App.Application.Context;
        var metadata = context.PackageManager?.GetApplicationInfo(context.PackageName!, Android.Content.PM.PackageInfoFlags.MetaData)?.MetaData;
        var key = metadata?.GetString("com.google.android.geo.API_KEY");
        return !string.IsNullOrWhiteSpace(key) && !key.Contains("NOT_CONFIGURED", StringComparison.Ordinal);
    }
#endif
}
