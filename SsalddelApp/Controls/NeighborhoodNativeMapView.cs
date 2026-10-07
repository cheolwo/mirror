using Ssalddel.Ui.Common.Areas.App.Models;

namespace SsalddelApp.Controls;

/// <summary>기사 업무에 종속되지 않는 Android Google 지도 표시 경계.</summary>
public sealed class NeighborhoodNativeMapView : View
{
    public static readonly BindableProperty StateProperty = BindableProperty.Create(nameof(State),
        typeof(NeighborhoodMapRenderState), typeof(NeighborhoodNativeMapView));
    public NeighborhoodMapRenderState? State
    {
        get => (NeighborhoodMapRenderState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private TaskCompletionSource<bool> _tilesLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> TilesLoaded => _tilesLoaded.Task;
    public event EventHandler<string>? MarkerSelected;
    public event EventHandler<NeighborhoodMapViewport>? ViewportChanged;
    internal void SelectMarker(string id) => MarkerSelected?.Invoke(this, id);
    internal void ChangeViewport(NeighborhoodMapViewport viewport) => ViewportChanged?.Invoke(this, viewport);
    internal void CompleteTiles(bool loaded) => _tilesLoaded.TrySetResult(loaded);
    internal void ResetTiles() => _tilesLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
