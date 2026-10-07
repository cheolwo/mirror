using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>입력 화면을 다녀오는 동안 유지하는 계정별 화면 문맥입니다. 원장·주소·GPS 응답은 보관하지 않습니다.</summary>
public sealed class NeighborhoodMapWorkspaceSession
{
    private bool _bound;
    private string? _owner;
    private NeighborhoodMapWorkspaceSnapshot? _snapshot;

    public void BindOwner(string? owner)
    {
        if (_bound && !string.Equals(_owner, owner, StringComparison.Ordinal)) _snapshot = null;
        _bound = true;
        _owner = owner;
    }

    public NeighborhoodMapWorkspaceSnapshot? Read(string? owner)
    {
        BindOwner(owner);
        return _snapshot;
    }

    public void Save(string? owner, NeighborhoodMapWorkspaceSnapshot snapshot)
    {
        BindOwner(owner);
        _snapshot = snapshot with { Layers = snapshot.Layers.ToArray() };
    }

    public void ClearPrivate(string? owner)
    {
        BindOwner(owner);
        if (_snapshot is not { } current) return;
        _snapshot = current with
        {
            SelectedRequestId = null,
            SelectedMarkerId = current.SelectedMarkerId?.StartsWith("delivery:", StringComparison.Ordinal) == true ? null : current.SelectedMarkerId,
            Viewport = current.PrivateViewport ? null : current.Viewport,
            PrivateViewport = false,
            MinePage = 1
        };
    }
}

public sealed record NeighborhoodMapWorkspaceSnapshot(
    IReadOnlyList<string> Layers,
    string ViewMode,
    string? RegionKey,
    int PostsPage,
    int MinePage,
    string? SelectedMarkerId,
    string? SelectedRequestId,
    NeighborhoodMapViewport? Viewport,
    bool PrivateViewport,
    string? PanelKind = null,
    string? PanelTarget = null,
    string PanelScope = "work",
    string? PanelRelated = null,
    int WorkListPage = 1,
    string WorkListScope = "requested",
    int DeliveryListPage = 1,
    int SpaceListPage = 1,
    bool SpaceListMine = false);
