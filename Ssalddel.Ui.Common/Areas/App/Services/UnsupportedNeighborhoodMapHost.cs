using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>지도 adapter가 없는 전문 역할 호스트에서도 기존 목록 업무를 보존합니다.</summary>
public sealed class UnsupportedNeighborhoodMapHost : INeighborhoodMapHost
{
    public Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state,
        Func<string, Task> markerSelected, CancellationToken cancellationToken = default)
        => Task.FromResult(NeighborhoodMapHostStatus.Unavailable);
    public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>기기 저장 adapter가 없는 호스트의 화면 수명 내 비민감 설정.</summary>
public sealed class VolatileNeighborhoodMapPreferenceStore : INeighborhoodMapPreferenceStore
{
    private readonly Dictionary<string, NeighborhoodMapPreferences> _preferences = new(StringComparer.Ordinal);
    public Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_preferences.GetValueOrDefault(ownerId ?? string.Empty));
    public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default)
    {
        _preferences[ownerId ?? string.Empty] = preferences;
        return Task.CompletedTask;
    }
}
