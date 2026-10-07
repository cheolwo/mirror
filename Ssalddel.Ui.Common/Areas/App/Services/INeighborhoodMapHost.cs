using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>Web Google 지도와 모바일 네이티브 지도는 각각 키·출처 제한을 유지합니다.</summary>
public interface INeighborhoodMapHost
{
    Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state,
        Func<string, Task> markerSelected, CancellationToken cancellationToken = default);
    Task HideAsync(CancellationToken cancellationToken = default);
}

/// <summary>지원하는 지도만 카메라와 앱 수명 변화를 전달합니다. 기존 표시 계약은 유지합니다.</summary>
public interface INeighborhoodMapInteractionHost
{
    event Action<NeighborhoodMapViewport>? ViewportChanged;
    event Action? Suspended;
    event Action? Invalidated;
}

public sealed record NeighborhoodMapPreferences(IReadOnlyList<string> Layers, string ViewMode, string? RegionKey);
public interface INeighborhoodMapPreferenceStore
{
    Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default);
    Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default);
}
