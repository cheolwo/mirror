using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>같은 계정의 역할별 화면 문맥과 미확정 명령 ID만 메모리에 보관합니다.</summary>
public sealed class RoleWorkspaceState
{
    private readonly Dictionary<string, RoleWorkspaceViewState> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Role, string Item, string Action), Guid> _requests = [];
    private bool _bound;
    private string? _owner;

    public string? LastRoleKey { get; private set; }

    public void BindOwner(string? owner)
    {
        if (_bound && !string.Equals(_owner, owner, StringComparison.Ordinal))
        {
            _views.Clear();
            _requests.Clear();
            LastRoleKey = null;
        }
        _bound = true;
        _owner = owner;
    }

    public RoleWorkspaceViewState Read(string roleKey)
        => _views.GetValueOrDefault(roleKey) ?? new(null, false, null);

    public void SelectRole(string roleKey)
    {
        if (RoleWorkspaceCatalog.Normalize(roleKey) is { } key) LastRoleKey = key;
    }

    public void Save(string roleKey, RoleWorkspaceViewState view)
    {
        if (RoleWorkspaceCatalog.Normalize(roleKey) is { } key)
            _views[key] = view with { SelectedId = RoleWorkspaceNavigation.StableId(view.SelectedId) };
    }

    public void ClearViewport(string roleKey)
    {
        if (_views.TryGetValue(roleKey, out var view)) _views[roleKey] = view with { Viewport = null };
    }

    public Guid RequestId(string roleKey, string itemId, string actionKey)
    {
        var key = (roleKey, itemId, actionKey);
        if (!_requests.TryGetValue(key, out var id)) _requests[key] = id = Guid.NewGuid();
        return id;
    }

    public void CompleteRequest(string roleKey, string itemId, string actionKey)
        => _requests.Remove((roleKey, itemId, actionKey));
}

public sealed record RoleWorkspaceViewState(string? SelectedId, bool DetailsExpanded, NeighborhoodMapViewport? Viewport,
    bool ShowMarkers = true, bool ShowRoutes = true, bool ListOnly = false);
