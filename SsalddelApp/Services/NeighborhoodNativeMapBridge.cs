using Ssalddel.Ui.Common.Areas.App.Models;

namespace SsalddelApp.Services;

public sealed record NeighborhoodMapBounds(double X, double Y, double Width, double Height,
    double ViewportWidth, bool Visible);

/// <summary>화면 경계와 지도 상태는 기기 안에서만 네이티브 지도에 전달합니다.</summary>
public sealed class NeighborhoodNativeMapBridge
{
    private MainPage? _page;
    public event Action<NeighborhoodMapViewport>? ViewportChanged;
    internal void Attach(MainPage page)
    {
        if (ReferenceEquals(_page, page)) return;
        if (_page is not null) _page.MapViewportChanged -= OnViewportChanged;
        _page = page; _page.MapViewportChanged += OnViewportChanged;
    }
    internal void Detach(MainPage page)
    {
        if (!ReferenceEquals(_page, page)) return;
        _page.MapViewportChanged -= OnViewportChanged; _page = null;
    }
    private void OnViewportChanged(NeighborhoodMapViewport viewport) => ViewportChanged?.Invoke(viewport);
    public Task<NeighborhoodMapHostStatus> RenderAsync(NeighborhoodMapBounds bounds, NeighborhoodMapRenderState state,
        Func<string, Task> selected, CancellationToken cancellationToken) => _page is null
        ? Task.FromResult(NeighborhoodMapHostStatus.Unavailable)
        : _page.ShowMapAsync(bounds, state, selected, cancellationToken);
    public Task MoveAsync(NeighborhoodMapBounds bounds) => MainThread.InvokeOnMainThreadAsync(() => _page?.MoveMap(bounds));
    public Task HideAsync() => MainThread.InvokeOnMainThreadAsync(() => _page?.HideMap());
}
