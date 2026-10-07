using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>역할 화면의 조회·선택·수명만 조립합니다. 권한과 업무 변경은 기존 API가 소유합니다.</summary>
public sealed class RoleWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyDictionary<string, IRoleWorkspaceAdapter> _adapters;
    private readonly IRoleWorkspaceAccess _access;
    private readonly RoleWorkspaceState _state;
    private readonly TimeProvider _clock;
    private DateTimeOffset _nextAutomaticRead;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _read;
    private CancellationTokenSource? _command;
    private IRoleWorkspaceAdapter? _adapter;
    private RoleWorkspaceIdentity? _identity;
    private RoleWorkspaceSnapshot? _snapshot;
    private long _generation, _readGeneration, _sceneRevision;
    private readonly Dictionary<string, int> _initializingAccess = new(StringComparer.Ordinal);
    private string? _refreshAfterInitializationRole;
    private bool _disposed, _suspended;

    public RoleWorkspaceViewModel(IEnumerable<IRoleWorkspaceAdapter> adapters, IRoleWorkspaceAccess access, RoleWorkspaceState state,
        TimeProvider? clock = null)
    {
        _adapters = adapters.ToDictionary(adapter => adapter.RoleKey, StringComparer.Ordinal);
        _access = access;
        _state = state;
        _clock = clock ?? TimeProvider.System;
        _access.Changed += AccessChanged;
    }

    public RoleWorkspaceDefinition? Role { get; private set; }
    public IReadOnlyList<RoleWorkspaceItem> Items => _snapshot?.Items ?? [];
    public RoleWorkspaceItem? SelectedItem => Items.FirstOrDefault(item => item.Id == SelectedId);
    public string? SelectedId { get; private set; }
    public string? Message => _snapshot?.Message;
    public IReadOnlyList<RoleWorkspaceAction> EmptyActions => _snapshot?.EmptyActions ?? [];
    public bool IsLoading { get; private set; }
    public bool IsRefreshing { get; private set; }
    public string? RefreshError { get; private set; }
    public string? LocationMessage { get; private set; }
    public bool CommandBusy { get; private set; }
    public bool AccountBusy { get; private set; }
    public bool IsAuthenticated => _identity?.IsAuthenticated == true;
    public string? AccountError { get; private set; }
    public bool DetailsExpanded { get; private set; }
    public bool ShowMarkers => Role is null || _state.Read(Role.Key).ShowMarkers;
    public bool ShowRoutes => Role is null || _state.Read(Role.Key).ShowRoutes;
    public bool ListOnly => Role is not null && _state.Read(Role.Key).ListOnly;
    public bool IsSuspended => _suspended;
    public bool RequiresLogin { get; private set; }
    public bool AccessDenied { get; private set; }
    public string? Error { get; private set; }
    public string? CommandError { get; private set; }
    public long Generation => _generation;
    public string WorkspaceHref => Role is null ? "/" : RoleWorkspaceNavigation.Href(Role.Key, SelectedId);

    public async Task InitializeAsync(string? roleKey, string? selectedId = null)
    {
        if (_disposed) return;
        var role = RoleWorkspaceCatalog.Find(roleKey);
        if (role?.Key != Role?.Key || Role is null)
        {
            _refreshAfterInitializationRole = null;
            if (Role is { } previous) _state.ClearViewport(previous.Key);
            Invalidate();
            Role = role;
            _adapter = role is null ? null : _adapters.GetValueOrDefault(role.Key);
            _identity = role is null ? null : _access.GetIdentity(role.Key);
            _state.BindOwner(_identity?.IsAuthenticated == true ? _identity.OwnerId : null);
            if (role is not null)
            {
                _state.SelectRole(role.Key);
                var view = _state.Read(role.Key);
                SelectedId = RoleWorkspaceNavigation.StableId(selectedId) ?? view.SelectedId;
                DetailsExpanded = view.DetailsExpanded;
            }
            Publish(true);
        }
        else
        {
            SynchronizeIdentity();
            if (RoleWorkspaceNavigation.StableId(selectedId) is { } id && id != SelectedId)
            {
                SelectedId = id;
                SaveView(null);
            }
            else if (_snapshot is not null || _suspended) return;
        }
        await RefreshAsync();
    }

    public Task RefreshAsync() => CommandBusy || AccountBusy ? Task.CompletedTask : RefreshCoreAsync(false);

    /// <summary>표시 중인 역할만 재조회하고 통신을 기다리지 않고 만료 위치를 제거합니다.</summary>
    public async Task TickAsync(bool allowRefresh = true)
    {
        if (_disposed || _suspended) return;
        SynchronizeIdentity();
        ExpireLocations();
        if (!allowRefresh || !IsAuthenticated || RequiresLogin || AccessDenied || IsLoading || IsRefreshing
            || CommandBusy || AccountBusy || _clock.GetUtcNow() < _nextAutomaticRead) return;
        await RefreshCoreAsync(true);
    }

    private async Task RefreshCoreAsync(bool background)
    {
        if (_disposed || _suspended) return;
        SynchronizeIdentity();
        if (Role is not { } role) { Error = "역할을 찾을 수 없습니다."; Publish(); return; }
        if (_adapter is not { } adapter) { Error = "이 역할의 업무 화면을 준비하지 못했습니다."; Publish(); return; }
        Cancel(ref _read);
        var read = _read = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var request = ++_readGeneration;
        var generation = _generation;
        var selected = SelectedId;
        var preserveSnapshot = background && _snapshot is not null;
        IsLoading = !preserveSnapshot;
        IsRefreshing = preserveSnapshot;
        Error = null;
        RefreshError = null;
        RequiresLogin = AccessDenied = false;
        if (!preserveSnapshot) _snapshot = null;
        Publish(true);
        try
        {
            _initializingAccess[role.Key] = _initializingAccess.GetValueOrDefault(role.Key) + 1;
            try { await _access.EnsureInitializedAsync(role.Key, read.Token); }
            finally { _initializingAccess[role.Key]--; }
            if (_refreshAfterInitializationRole == role.Key && Role?.Key == role.Key && !_suspended && !_disposed)
            {
                _refreshAfterInitializationRole = null;
                await RefreshCoreAsync(background);
                return;
            }
            if (!ScopeCurrent(generation, role.Key) || request != _readGeneration) return;
            var identity = _access.GetIdentity(role.Key);
            if (_identity != identity)
            {
                SynchronizeIdentity();
                await RefreshCoreAsync(background);
                return;
            }
            var snapshot = await adapter.LoadAsync(selected, read.Token);
            if (!Current(generation, role.Key) || request != _readGeneration) return;
            if (!string.Equals(snapshot.RoleKey, role.Key, StringComparison.Ordinal))
                throw new InvalidOperationException("역할별 응답이 일치하지 않습니다.");
            _snapshot = snapshot with { Items = snapshot.Items.ToArray() };
            var preferred = Items.FirstOrDefault(item => item.Id == snapshot.SelectedId)
                ?? Items.FirstOrDefault(item => item.Id == selected);
            var current = Items.FirstOrDefault(item => item.IsCurrent);
            SelectedId = (role.Key is RoleWorkspaceCatalog.FoodDriver or RoleWorkspaceCatalog.CargoDriver or RoleWorkspaceCatalog.Warehouse)
                && preferred?.IsCurrent != true && current is not null ? current.Id : preferred?.Id ?? current?.Id ?? Items.FirstOrDefault()?.Id;
            if (SelectedId != selected) _state.ClearViewport(role.Key);
            LocationMessage = null;
            ExpireLocations();
            SaveView(_state.Read(role.Key).Viewport);
        }
        catch (OperationCanceledException) when (read.IsCancellationRequested)
        {
            if (_refreshAfterInitializationRole == role.Key && _initializingAccess.GetValueOrDefault(role.Key) == 0 && Role?.Key == role.Key && !_suspended && !_disposed)
            {
                _refreshAfterInitializationRole = null;
                await RefreshAsync();
            }
        }
        catch (RoleWorkspaceAccessException exception)
        {
            if (Current(generation, role.Key) && request == _readGeneration)
            {
                RequiresLogin = exception.StatusCode == 401;
                AccessDenied = exception.StatusCode == 403;
                if (RequiresLogin || AccessDenied || !preserveSnapshot)
                { _snapshot = null; Error = exception.Message; adapter.Clear(); }
                else RefreshError = "업무 정보를 갱신하지 못했습니다. 마지막 확인 내용을 표시하고 있습니다.";
            }
        }
        catch (Exception)
        {
            if (Current(generation, role.Key) && request == _readGeneration)
            {
                if (preserveSnapshot) RefreshError = "업무 정보를 갱신하지 못했습니다. 마지막 확인 내용을 표시하고 있습니다.";
                else { Error = "현재 업무를 불러오지 못했습니다. 다시 시도해 주세요."; adapter.Clear(); }
            }
        }
        finally
        {
            if (Current(generation, role.Key) && request == _readGeneration)
            {
                IsLoading = false;
                IsRefreshing = false;
                _nextAutomaticRead = _clock.GetUtcNow().AddSeconds(15);
                Publish(true);
            }
        }
    }

    public async Task SelectAsync(string itemId)
    {
        if (_disposed || _suspended || CommandBusy || AccountBusy || IsRefreshing || !Items.Any(item => item.Id == itemId)) return;
        SelectedId = itemId;
        CommandError = null;
        SaveView(null);
        await RefreshAsync();
    }

    public Task SelectMarkerAsync(string markerId)
    {
        var item = Items.FirstOrDefault(item => item.Markers?.Any(marker => marker.Id == markerId) == true);
        return item is null ? Task.CompletedTask : SelectAsync(item.Id);
    }

    public async Task PerformAsync(string itemId, string actionKey)
    {
        SynchronizeIdentity();
        var action = itemId.Length == 0 ? EmptyActions.FirstOrDefault(action => action.Key == actionKey)
            : SelectedItem?.Id == itemId ? SelectedItem.Actions?.FirstOrDefault(action => action.Key == actionKey) : null;
        if (_disposed || _suspended || CommandBusy || AccountBusy || IsLoading || IsRefreshing || RefreshError is not null || Role is not { } role || _adapter is not { } adapter
            || action is not { Enabled: true, Route: null }) return;
        var generation = _generation;
        var requestId = _state.RequestId(role.Key, itemId, actionKey);
        var command = _command = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CommandBusy = true;
        CommandError = null;
        Publish();
        try
        {
            await adapter.PerformAsync(itemId, actionKey, requestId, command.Token);
            if (!Current(generation, role.Key)) return;
            _state.CompleteRequest(role.Key, itemId, actionKey);
            // 처리 결과 확인은 명령 내부에서 한 번만 수행합니다. 지도 무효화 등 외부 조회는 수행 중 차단합니다.
            await RefreshCoreAsync(false);
        }
        catch (OperationCanceledException) when (command.IsCancellationRequested) { }
        catch (RoleWorkspaceAccessException exception)
        {
            if (Current(generation, role.Key))
            {
                RequiresLogin = exception.StatusCode == 401;
                AccessDenied = exception.StatusCode == 403;
                CommandError = exception.Message;
                if (RequiresLogin || AccessDenied)
                { _snapshot = null; adapter.Clear(); Error = exception.Message; CommandError = null; Publish(true); }
            }
        }
        catch (Exception)
        {
            if (Current(generation, role.Key)) CommandError = "처리 결과를 확인하지 못했습니다. 같은 버튼으로 다시 확인해 주세요.";
        }
        finally
        {
            if (Current(generation, role.Key)) { CommandBusy = false; Publish(); }
            if (ReferenceEquals(_command, command)) _command = null;
            command.Dispose();
        }
    }

    public void SetDetailsExpanded(bool expanded)
    {
        DetailsExpanded = expanded;
        SaveView(Role is { } role ? _state.Read(role.Key).Viewport : null);
        Publish();
    }

    public async Task SignOutAsync()
    {
        SynchronizeIdentity();
        if (_disposed || _suspended || CommandBusy || AccountBusy || !IsAuthenticated || Role is not { } role) return;
        var generation = _generation;
        AccountBusy = true; AccountError = null; Publish();
        try
        {
            await _access.SignOutAsync(role.Key, _lifetime.Token);
            // 계정 변경 알림은 이전 DTO와 지도를 비우고 최신 권한으로 다시 조회합니다.
            if (Current(generation, role.Key)) await RefreshCoreAsync(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            if (Current(generation, role.Key)) AccountError = "로그아웃하지 못했습니다. 다시 시도해 주세요.";
        }
        finally
        {
            if (Current(generation, role.Key)) { AccountBusy = false; Publish(); }
        }
    }

    public void SetViewport(NeighborhoodMapViewport viewport)
    {
        if (_disposed || _suspended || !ValidPoint(viewport.Latitude, viewport.Longitude) || !double.IsFinite(viewport.Zoom)) return;
        SaveView(viewport with { Zoom = Math.Clamp(viewport.Zoom, 3, 20) });
    }

    public void SetMapVisibility(bool markers, bool routes)
    {
        if (Role is not { } role || _disposed) return;
        _state.Save(role.Key, _state.Read(role.Key) with { ShowMarkers = markers, ShowRoutes = routes });
        Publish(true);
    }

    public void SetListOnly(bool listOnly)
    {
        if (_disposed || Role is not { } role) return;
        _state.Save(role.Key, _state.Read(role.Key) with { ListOnly = listOnly });
        Publish(true);
    }

    public NeighborhoodMapRenderState RenderState()
    {
        if (_disposed || _suspended || Role is null) return new(_sceneRevision, [], []);
        var markers = Items.SelectMany(item => (item.Markers ?? []).Select(marker => item.Id == SelectedId
                ? marker : marker with { Kind = NeighborhoodMapMarkerKinds.Inactive }))
            .Where(marker => ValidPoint(marker.Latitude, marker.Longitude) && IsUnexpired(marker.ExpiresAt)).DistinctBy(marker => marker.Id).ToArray();
        var routes = Items.SelectMany(item => (item.Routes ?? []).Select(route => item.Id == SelectedId
                ? route : route with { Color = "#6b7280" }))
            .Where(route => IsUnexpired(route.ExpiresAt) && route.Points.Count >= 2 && route.Points.All(point => ValidPoint(point.Latitude, point.Longitude)))
            .DistinctBy(route => route.Id).ToArray();
        var viewport = _state.Read(Role.Key).Viewport;
        if (viewport is null)
        {
            var selected = SelectedItem?.Markers?.Where(marker => ValidPoint(marker.Latitude, marker.Longitude) && IsUnexpired(marker.ExpiresAt)).ToArray() ?? [];
            if (selected.Length > 0)
            {
                var span = Math.Max(selected.Max(marker => marker.Latitude) - selected.Min(marker => marker.Latitude),
                    selected.Max(marker => marker.Longitude) - selected.Min(marker => marker.Longitude));
                viewport = new(selected.Average(marker => marker.Latitude), selected.Average(marker => marker.Longitude),
                    Math.Clamp(Math.Log2(360 / Math.Max(0.004, span)) - 1, 3, 16));
            }
        }
        return new(_sceneRevision, ShowMarkers ? markers : [], ShowRoutes ? routes : [],
            ShowMarkers ? markers.FirstOrDefault(marker => SelectedItem?.Markers?.Any(value => value.Id == marker.Id) == true)?.Id : null, viewport, _state.Read(Role.Key).Viewport is not null);
    }

    private bool IsUnexpired(DateTimeOffset? expiresAt) => expiresAt is null || expiresAt > _clock.GetUtcNow();

    private void ExpireLocations()
    {
        if (_snapshot is null) return;
        var expired = false;
        var items = _snapshot.Items.Select(item =>
        {
            var markers = item.Markers?.Where(marker => IsUnexpired(marker.ExpiresAt)).ToArray();
            var routes = item.Routes?.Where(route => IsUnexpired(route.ExpiresAt)).ToArray();
            if (markers?.Length == item.Markers?.Count && routes?.Length == item.Routes?.Count) return item;
            expired = true;
            return item with { Markers = markers, Routes = routes };
        }).ToArray();
        if (!expired) return;
        _snapshot = _snapshot with { Items = items };
        LocationMessage = "최근 위치 정보의 유효 시간이 지났습니다. 새 위치를 확인해 주세요.";
        Publish(true);
    }

    public void Suspend()
    {
        if (_disposed) return;
        Invalidate();
        _suspended = true;
        if (Role is { } role) _state.ClearViewport(role.Key);
        Publish(true);
    }

    public async Task ResumeAsync()
    {
        if (_disposed) return;
        _suspended = false;
        SynchronizeIdentity();
        await RefreshAsync();
    }

    public void SynchronizeIdentity()
    {
        if (_disposed || Role is not { } role) return;
        var current = _access.GetIdentity(role.Key);
        if (_identity == current) return;
        Invalidate();
        _identity = current;
        _state.BindOwner(current.IsAuthenticated ? current.OwnerId : null);
        _state.ClearViewport(role.Key);
        SelectedId = _state.Read(role.Key).SelectedId;
        DetailsExpanded = _state.Read(role.Key).DetailsExpanded;
        Publish(true);
    }

    private void AccessChanged()
    {
        var generation = _generation;
        SynchronizeIdentity();
        if (!_disposed && !_suspended && Role is not null && generation != _generation)
        {
            if (_initializingAccess.GetValueOrDefault(Role.Key) > 0) _refreshAfterInitializationRole = Role.Key;
            else _ = RefreshAsync();
        }
    }

    private bool Current(long generation, string roleKey)
        => ScopeCurrent(generation, roleKey) && _identity == _access.GetIdentity(roleKey);

    private bool ScopeCurrent(long generation, string roleKey)
        => !_disposed && !_suspended && generation == _generation && Role?.Key == roleKey;

    private void Invalidate()
    {
        ++_generation;
        ++_readGeneration;
        Cancel(ref _read);
        Cancel(ref _command);
        _adapter?.Clear();
        _snapshot = null;
        IsLoading = IsRefreshing = CommandBusy = AccountBusy = RequiresLogin = AccessDenied = false;
        Error = CommandError = AccountError = RefreshError = LocationMessage = null;
    }

    private void SaveView(NeighborhoodMapViewport? viewport)
    {
        if (Role is { } role) _state.Save(role.Key,
            _state.Read(role.Key) with { SelectedId = SelectedId, DetailsExpanded = DetailsExpanded, Viewport = viewport });
    }

    private void Publish(bool sceneChanged = false)
    {
        if (sceneChanged) ++_sceneRevision;
        OnPropertyChanged(string.Empty);
    }

    private static bool ValidPoint(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static void Cancel(ref CancellationTokenSource? cancellation)
    {
        var source = cancellation;
        cancellation = null;
        source?.Cancel();
        source?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _access.Changed -= AccessChanged;
        Invalidate();
        if (Role is { } role) _state.ClearViewport(role.Key);
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
