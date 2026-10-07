using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Contracts.Common.PublicData;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodExchangeMapViewModel(
    INeighborhoodExchangeMapClient client, INeighborhoodMapPreferenceStore preferences,
    ISsalddel현재사용자Context user, TimeProvider? clock = null, NeighborhoodMapWorkspaceSession? session = null) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<string> _layers = new(StringComparer.Ordinal);
    private string? _owner;
    private readonly NeighborhoodMapWorkspaceSession _workspace = session ?? new();
    private readonly NeighborhoodMapLayerRequest _publicRequests = new(), _mapRequests = new(), _postsRequests = new(),
        _mineRequests = new(), _deliveryRequests = new(), _publicDataRequests = new(), _storageRequests = new();
    private long _revision;
    private NeighborhoodMapViewport? _viewport;
    private bool _privateViewport, _privateReadSuspended;
    private long _initializationGeneration;
    private bool _initialized, _disposed;
    private readonly SemaphoreSlim _preferenceWrites = new(1, 1);
    private NeighborhoodExchangeMapResponse _map = new();
    public IReadOnlyList<NeighborhoodPublicRegionDto> Regions { get; private set; } = [];
    public IReadOnlyList<RegionalAgriculturalMapMarkerDto> PublicDataMarkers { get; private set; } = [];
    public RegionalAgriculturalMapMarkerDto? SelectedPublicData { get; private set; }
    public bool PublicDataLoading { get; private set; }
    public string? PublicDataError { get; private set; }
    public IReadOnlyList<NeighborhoodStorageMapMarkerDto> StorageMarkers { get; private set; } = [];
    public bool StorageLoading { get; private set; }
    public string? StorageError { get; private set; }
    public int SelectedStorageCount => HasLayer(NeighborhoodMapNavigation.Storage)
        ? StorageMarkers.Where(item => item.Region.RegionKey == RegionKey).Sum(item => item.SpaceCount) : 0;
    public IReadOnlyList<PlatformCommunityPostResponse> Posts { get; private set; } = [];
    public IReadOnlyList<NeighborhoodMapDeliverySummary> Deliveries { get; private set; } = [];
    public NeighborhoodDeliveryMapResponse? Delivery { get; private set; }
    public string? SelectedRequestId { get; private set; }
    public string? RegionKey { get; private set; }
    public string? SelectedMarkerId { get; private set; }
    public string ViewMode { get; private set; } = "map";
    public string? PanelKind { get; private set; }
    public string? PanelTarget { get; private set; }
    public string? PanelRelated { get; private set; }
    public string PanelScope { get; private set; } = "work";
    public int WorkListPage { get; private set; } = 1;
    public string WorkListScope { get; private set; } = "requested";
    public int DeliveryListPage { get; private set; } = 1;
    public int SpaceListPage { get; private set; } = 1;
    public bool SpaceListMine { get; private set; }
    public bool IsAuthenticated => CurrentOwner is not null && !RequiresLogin;
    public bool RequiresLogin { get; private set; }
    public bool IsLoading { get; private set; }
    public bool PostsLoading { get; private set; }
    public bool MineLoading { get; private set; }
    public bool DeliveryLoading { get; private set; }
    public string? Error { get; private set; }
    public string? PostsError { get; private set; }
    public string? MineError { get; private set; }
    public string? DeliveryError { get; private set; }
    public string? PreferenceError { get; private set; }
    public int Page { get; private set; } = 1;
    public int TotalCount { get; private set; }
    public bool HasNext => Page * NeighborhoodExchange.PageSize < TotalCount;
    public int MinePage { get; private set; } = 1;
    public bool MineHasNext { get; private set; }
    public bool PreserveViewport { get; private set; }
    public IReadOnlyList<string> SelectedLayers => NeighborhoodMapNavigation.LayerCodes.Where(_layers.Contains).ToArray();
    public string HomeHref => NeighborhoodMapNavigation.Href(SelectedLayers, ViewMode, RegionKey, PanelKind, PanelTarget, PanelRelated);
    public string WriteHref => NeighborhoodMapNavigation.WriteHref(RegionKey, HomeHref);
    public NeighborhoodPublicRegionDto? SelectedRegion => Regions.FirstOrDefault(region => region.RegionKey == RegionKey);
    public int UnlocatedCount => _map.UnlocatedPostCount;
    public int UnsupportedCount => _map.UnsupportedRegionPostCount;
    public string Boundary => _map.LocationBoundary;
    public bool HasLayer(string code) => _layers.Contains(code);
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    private DateTime UtcNow => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public bool SynchronizeOwner()
    {
        if (_disposed) return false;
        var owner = CurrentOwner;
        _workspace.BindOwner(owner);
        if (string.Equals(_owner, owner, StringComparison.Ordinal)) return false;
        _owner = owner; _initialized = false; RequiresLogin = false; ClearPrivate(false);
        _viewport = null; _privateViewport = false; PanelKind = null; PanelTarget = null; PanelRelated = null;
        WorkListPage = DeliveryListPage = SpaceListPage = 1; WorkListScope = "requested"; SpaceListMine = false;
        Changed(true); return true;
    }
    public void ResetAuthentication()
    {
        if (_disposed) return;
        SynchronizeOwner();
        RequiresLogin = false; _initialized = false; ++_initializationGeneration; ClearPrivate(); Changed(true);
    }
    private void ClearPrivate(bool clearSession = true)
    {
        _mineRequests.Invalidate(); _deliveryRequests.Invalidate();
        Deliveries = []; Delivery = null; SelectedRequestId = null;
        if (SelectedMarkerId?.StartsWith("delivery:", StringComparison.Ordinal) == true) SelectedMarkerId = null;
        MineLoading = false; DeliveryLoading = false; MineHasNext = false; MinePage = 1;
        MineError = null; DeliveryError = null;
        if (_privateViewport) { _viewport = null; _privateViewport = false; }
        if (clearSession) _workspace.ClearPrivate(_owner);
    }
    public async Task InitializeAsync(string? layers, string? view, string? region, string? panel = null, string? target = null, string? related = null)
    {
        SynchronizeOwner();
        if (_disposed) return;
        var initialization = ++_initializationGeneration;
        var owner = _owner;
        var remembered = !_initialized ? _workspace.Read(owner) : null;
        NeighborhoodMapPreferences? stored = null;
        if (!_initialized)
        {
            try { stored = await preferences.LoadAsync(owner, _lifetime.Token); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            catch (Exception) { if (!_disposed && _owner == owner) PreferenceError = "저장한 화면 설정을 불러오지 못했습니다."; }
            if (_disposed || _owner != owner || initialization != _initializationGeneration) return;
        }
        var selected = layers is not null ? NeighborhoodMapNavigation.ParseLayers(layers)
            : _initialized ? SelectedLayers : remembered?.Layers ?? stored?.Layers?.Where(NeighborhoodMapNavigation.LayerCodes.Contains).ToArray() ?? NeighborhoodMapNavigation.DefaultLayers;
        var nextView = NeighborhoodMapNavigation.View(view ?? (_initialized ? ViewMode : remembered?.ViewMode ?? stored?.ViewMode));
        var nextRegion = region ?? (_initialized ? RegionKey : remembered?.RegionKey ?? stored?.RegionKey);
        // 생성된 지도 URL은 닫힌 패널도 명시합니다. 쿼리 없는 지도 진입만 메모리 문맥을 복원합니다.
        var explicitNavigation = layers is not null || view is not null || region is not null || panel is not null || target is not null || related is not null;
        var nextPanel = explicitNavigation ? NeighborhoodMapNavigation.PanelKind(panel) : _initialized ? PanelKind : remembered?.PanelKind;
        var nextTarget = nextPanel is "post" or "work" or "space" or "delivery"
            ? NeighborhoodMapNavigation.PanelTarget(explicitNavigation ? target : _initialized ? PanelTarget : remembered?.PanelTarget) : null;
        var nextRelated = nextPanel == "space" && nextTarget is not null
            ? NeighborhoodMapNavigation.PanelTarget(explicitNavigation ? related : _initialized ? PanelRelated : remembered?.PanelRelated) : null;
        if (_initialized && _layers.SetEquals(selected) && ViewMode == nextView && RegionKey == nextRegion)
        {
            SelectPanel(nextPanel, nextTarget, nextRelated);
            if (nextPanel == "delivery" && nextTarget is not null && SelectedRequestId != nextTarget)
                await SelectDeliveryAsync(nextTarget, requireKnown: false);
            return;
        }
        _layers.Clear(); _layers.UnionWith(selected); ViewMode = nextView; RegionKey = nextRegion;
        PanelKind = nextPanel; PanelTarget = nextTarget; PanelRelated = nextRelated; PanelScope = remembered?.PanelScope ?? PanelScope;
        if (nextPanel is "work" or "delivery") PanelScope = nextPanel;
        WorkListPage = remembered?.WorkListPage ?? WorkListPage; WorkListScope = remembered?.WorkListScope ?? WorkListScope;
        DeliveryListPage = remembered?.DeliveryListPage ?? DeliveryListPage;
        SpaceListPage = remembered?.SpaceListPage ?? SpaceListPage; SpaceListMine = remembered?.SpaceListMine ?? SpaceListMine;
        // 이전 개인 화면의 카메라는 새 서버 권한 조회가 성공한 뒤에만 다시 사용합니다.
        var sameRegion = region is null || region == remembered?.RegionKey;
        _viewport = remembered?.PrivateViewport == true || !sameRegion ? null : remembered?.Viewport; _privateViewport = false;
        PreserveViewport = _viewport is not null;
        SelectedMarkerId = sameRegion ? remembered?.SelectedMarkerId : null;
        var restoreRequest = nextPanel == "delivery" ? nextTarget : !explicitNavigation ? remembered?.SelectedRequestId : null;
        if (!HasLayer(NeighborhoodMapNavigation.Mine)) { ClearPrivate(); restoreRequest = null; }
        if (!HasLayer(NeighborhoodMapNavigation.PublicData)) { _publicDataRequests.Invalidate(); PublicDataMarkers = []; SelectedPublicData = null; PublicDataLoading = false; PublicDataError = null; }
        if (!HasLayer(NeighborhoodMapNavigation.Storage)) { _storageRequests.Invalidate(); StorageMarkers = []; StorageLoading = false; StorageError = null; }
        _initialized = true;
        Changed(true);
        await RefreshPublicAsync(sameRegion ? remembered?.PostsPage ?? Page : 1, remembered?.MinePage ?? 1);
        if (_disposed || initialization != _initializationGeneration || _owner != owner) return;
        // 개인 선택은 ID만 복원하며 현재 본인 목록과 서버 권한을 새로 확인합니다.
        if (restoreRequest is not null && IsAuthenticated && HasLayer(NeighborhoodMapNavigation.Mine)
            && (nextPanel == "delivery" || Deliveries.Any(item => item.RequestId == restoreRequest)) && PanelKind is null or "delivery")
        {
            var camera = sameRegion && (remembered?.PrivateViewport != true || remembered.SelectedRequestId == restoreRequest)
                ? remembered?.Viewport : null;
            await SelectDeliveryAsync(restoreRequest, requireKnown: nextPanel != "delivery");
            if (_disposed || initialization != _initializationGeneration || _owner != owner) return;
            if (Delivery is not null && camera is not null) { _viewport = camera; _privateViewport = true; PreserveViewport = true; Changed(true); }
        }
        else if (SelectedMarkerId?.StartsWith("delivery:", StringComparison.Ordinal) == true)
        {
            SelectedMarkerId = null;
            if (_privateViewport) { _viewport = null; _privateViewport = false; PreserveViewport = false; }
            Changed(true);
        }
        if (SelectedMarkerId?.StartsWith("public-data:", StringComparison.Ordinal) == true)
            SelectedPublicData = PublicDataMarkers.FirstOrDefault(item => "public-data:" + item.MarkerKey == SelectedMarkerId);
        if (restoreRequest is null && remembered?.PrivateViewport == true && sameRegion && MineError is null
            && IsAuthenticated && HasLayer(NeighborhoodMapNavigation.Mine)
            && Deliveries.Any(item => item.Pickup is not null || item.Dropoff is not null))
        {
            _viewport = remembered.Viewport; _privateViewport = true; PreserveViewport = true; Changed(true);
        }
    }
    public async Task RefreshPublicAsync(int? postsPage = null, int minePage = 1)
    {
        if (_disposed) return;
        var read = _publicRequests.Begin(_lifetime.Token);
        _mapRequests.Invalidate(); _postsRequests.Invalidate(); _publicDataRequests.Invalidate(); _storageRequests.Invalidate();
        PostsLoading = PublicDataLoading = StorageLoading = false;
        IsLoading = true; Error = null; Changed();
        try
        {
            var regions = await client.RegionsAsync(read.Token);
            if (!PublicCurrent(read)) return;
            Regions = regions.Items.Where(IsPublicRegion).DistinctBy(region => region.RegionKey).ToArray();
            if (RegionKey is not null && !Regions.Any(region => region.RegionKey == RegionKey)) RegionKey = null;
            if (SelectedMarkerId is null || SelectedMarkerId.StartsWith("region:", StringComparison.Ordinal))
                SelectedMarkerId = RegionKey is null ? null : "region:" + RegionKey;
            Changed(true);
            // 검증된 대표점 이후 각 레이어를 병렬로 조회합니다. 한 레이어 실패가 다른 결과를 막지 않습니다.
            var pending = new List<Task> { LoadMapAsync(), LoadPostsAsync(postsPage ?? Page) };
            if (HasLayer(NeighborhoodMapNavigation.PublicData)) pending.Add(LoadPublicDataAsync());
            if (HasLayer(NeighborhoodMapNavigation.Storage)) pending.Add(LoadStorageAsync());
            if (IsAuthenticated && HasLayer(NeighborhoodMapNavigation.Mine)) pending.Add(LoadMineAsync(minePage));
            await Task.WhenAll(pending);
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception) { if (PublicCurrent(read)) Error = "동네 정보를 불러오지 못했습니다. 연결을 확인하고 다시 시도해 주세요."; }
        finally { if (PublicCurrent(read)) { IsLoading = false; Changed(); } }
    }
    private async Task LoadMapAsync()
    {
        var read = _mapRequests.Begin(_lifetime.Token);
        _map = new();
        if (!HasLayer(NeighborhoodMapNavigation.Offer) && !HasLayer(NeighborhoodMapNavigation.Need)) { Changed(true); return; }
        try
        {
            var map = await client.MapAsync(IntentFilter, read.Token);
            if (_disposed || !_mapRequests.IsCurrent(read)) return;
            var known = Regions.ToDictionary(region => region.RegionKey, StringComparer.Ordinal);
            _map = new() { Items = map.Items.Where(item => known.ContainsKey(item.Region.RegionKey))
                .Select(item => new NeighborhoodExchangeMapMarkerDto { Region = known[item.Region.RegionKey], OfferCount = Math.Max(0, item.OfferCount), NeedCount = Math.Max(0, item.NeedCount) }).ToArray(),
                UnlocatedPostCount = map.UnlocatedPostCount, UnsupportedRegionPostCount = map.UnsupportedRegionPostCount, LocationBoundary = map.LocationBoundary };
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && _mapRequests.IsCurrent(read)) Error = "동네 지도를 불러오지 못했습니다. 다시 시도해 주세요."; }
        finally { if (!_disposed && _mapRequests.IsCurrent(read)) Changed(true); }
    }
    private string? IntentFilter => HasLayer(NeighborhoodMapNavigation.Offer) == HasLayer(NeighborhoodMapNavigation.Need)
        ? null : HasLayer(NeighborhoodMapNavigation.Offer) ? NeighborhoodExchange.Offer : NeighborhoodExchange.Need;
    public async Task LoadPostsAsync(int page)
    {
        if (_disposed) return;
        var read = _postsRequests.Begin(_lifetime.Token); Page = Math.Clamp(page, 1, 10000); Posts = []; TotalCount = 0; PostsError = null;
        if (!HasLayer(NeighborhoodMapNavigation.Offer) && !HasLayer(NeighborhoodMapNavigation.Need)) { PostsLoading = false; Changed(); return; }
        var region = RegionKey; var intent = IntentFilter; PostsLoading = true; Changed();
        try
        {
            var result = await client.PostsAsync(region, intent, Page, read.Token);
            if (_disposed || !_postsRequests.IsCurrent(read)) return;
            Posts = result.Items.Where(NeighborhoodExchange.IsExchange).ToArray(); TotalCount = result.TotalCount;
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && _postsRequests.IsCurrent(read)) PostsError = "글 목록을 불러오지 못했습니다. 다시 시도해 주세요."; }
        finally { if (!_disposed && _postsRequests.IsCurrent(read)) { PostsLoading = false; Changed(); } }
    }
    public async Task ToggleLayerAsync(string code)
    {
        if (_disposed || !NeighborhoodMapNavigation.LayerCodes.Contains(code)) return;
        if (!_layers.Add(code)) _layers.Remove(code);
        PreserveViewport = true;
        if (code == NeighborhoodMapNavigation.Mine)
        { if (!HasLayer(code)) ClearPrivate(); else await LoadMineAsync(); }
        else if (code == NeighborhoodMapNavigation.PublicData)
        {
            if (HasLayer(code)) await LoadPublicDataAsync();
            else { _publicDataRequests.Invalidate(); PublicDataMarkers = []; SelectedPublicData = null; PublicDataLoading = false; PublicDataError = null; }
        }
        else if (code == NeighborhoodMapNavigation.Storage)
        {
            if (HasLayer(code)) await LoadStorageAsync();
            else { _storageRequests.Invalidate(); StorageMarkers = []; StorageLoading = false; StorageError = null; }
        }
        else await RefreshPublicAsync();
        await SaveAsync(); Changed(true);
    }
    public async Task SetViewAsync(string mode) { ViewMode = NeighborhoodMapNavigation.View(mode); PreserveViewport = true; await SaveAsync(); Changed(); }
    public async Task SelectRegionAsync(string? regionKey)
    {
        if (_disposed) return;
        RegionKey = Regions.Any(value => value.RegionKey == regionKey) ? regionKey : null;
        SelectedPublicData = null;
        _deliveryRequests.Invalidate(); Delivery = null; SelectedRequestId = null; DeliveryError = null; DeliveryLoading = false;
        SelectedMarkerId = RegionKey is null ? null : "region:" + RegionKey; PreserveViewport = false;
        _viewport = null; _privateViewport = false; PanelKind = null; PanelTarget = null; PanelRelated = null;
        Changed(true); await LoadPostsAsync(1); await SaveAsync();
    }
    public Task SelectMarkerAsync(string id)
    {
        if (id.StartsWith("region:", StringComparison.Ordinal)) return SelectRegionAsync(id[7..]);
        if (id.StartsWith("delivery:", StringComparison.Ordinal) && id.LastIndexOf(':') > 9) return SelectDeliveryAsync(id[9..id.LastIndexOf(':')]);
        if (id.StartsWith("public-data:", StringComparison.Ordinal))
        { ClearSelection(); SelectedPublicData = PublicDataMarkers.FirstOrDefault(item => item.MarkerKey == id[12..]); SelectedMarkerId = id; PreserveViewport = false; _viewport = null; _privateViewport = false; PanelKind = null; PanelTarget = null; PanelRelated = null; Changed(true); }
        return Task.CompletedTask;
    }
    public void ClearSelection()
    {
        if (_disposed) return;
        var sceneChanged = Delivery is not null || SelectedRequestId is not null || SelectedPublicData is not null;
        var selectedObjectChanged = sceneChanged;
        _deliveryRequests.Invalidate(); Delivery = null; SelectedRequestId = null; DeliveryError = null; DeliveryLoading = false;
        SelectedPublicData = null;
        // 회색 본인 배송 핀만 보던 카메라는 패널만 바꿀 때 유지합니다.
        // 실제 선택 배송·공공자료를 닫을 때는 그 선택의 카메라 문맥을 끝냅니다.
        if (selectedObjectChanged) { sceneChanged |= _viewport is not null; _viewport = null; _privateViewport = false; }
        var marker = RegionKey is null ? null : "region:" + RegionKey;
        sceneChanged |= SelectedMarkerId != marker;
        SelectedMarkerId = marker; PreserveViewport = true; Changed(sceneChanged);
    }
    public async Task LoadMineAsync(int page = 1)
    {
        SynchronizeOwner();
        if (_disposed || _privateReadSuspended || !IsAuthenticated || !HasLayer(NeighborhoodMapNavigation.Mine)) return;
        var read = _mineRequests.Begin(_lifetime.Token); var owner = _owner;
        MineLoading = true; MineError = null; MinePage = Math.Clamp(page, 1, 10000); Changed();
        try
        {
            var result = await client.MineAsync(MinePage, read.Token);
            if (!PrivateCurrent(read, owner, _mineRequests)) return;
            Deliveries = page == 1 ? result.Items : Deliveries.Concat(result.Items).DistinctBy(value => value.RequestId).ToArray();
            MineHasNext = result.HasNext;
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!PrivateCurrent(read, owner, _mineRequests)) return;
            if (ex is SsalddelApiException { StatusCode: 401 }) { RequiresLogin = true; ClearPrivate(); }
            else if (ex is SsalddelApiException { StatusCode: 403 }) ClearPrivate();
            MineError = "내 배송 목록을 확인하지 못했습니다. 로그인과 연결을 확인해 주세요.";
        }
        finally { if (PrivateCurrent(read, owner, _mineRequests)) { MineLoading = false; Changed(true); } else if (!_disposed) Changed(true); }
    }
    public async Task SelectDeliveryAsync(string requestId, bool requireKnown = true)
    {
        SynchronizeOwner();
        if (_disposed || _privateReadSuspended || !IsAuthenticated || !HasLayer(NeighborhoodMapNavigation.Mine)
            || NeighborhoodMapNavigation.PanelTarget(requestId) is null
            || (requireKnown && !Deliveries.Any(value => value.RequestId == requestId))) return;
        _deliveryRequests.Invalidate(); SelectedRequestId = requestId; Delivery = null; DeliveryError = null; DeliveryLoading = false;
        SelectedPublicData = null;
        SelectedMarkerId = "delivery:" + requestId + ":pickup"; PreserveViewport = false; _viewport = null; _privateViewport = false;
        PanelKind = "delivery"; PanelTarget = requestId; PanelRelated = null; PanelScope = "delivery"; Changed(true);
        await RefreshDeliveryAsync(true);
    }
    public async Task RefreshDeliveryAsync(bool includeRoute)
    {
        SynchronizeOwner();
        if (_disposed || _privateReadSuspended || !IsAuthenticated || !HasLayer(NeighborhoodMapNavigation.Mine) || SelectedRequestId is not { } id || DeliveryLoading) return;
        var read = _deliveryRequests.Begin(_lifetime.Token); var owner = _owner;
        if (!includeRoute) PreserveViewport = true;
        var previous = Delivery; DeliveryLoading = true; DeliveryError = null; Changed();
        try
        {
            var value = await client.DeliveryMapAsync(id, includeRoute, read.Token);
            if (!PrivateCurrent(read, owner, _deliveryRequests) || SelectedRequestId != id) return;
            if (value is null || value.RequestId != id) { Delivery = null; ClearPrivateCamera(); DeliveryError = "이 배송의 지도를 확인할 수 없습니다."; return; }
            if (value.DriverLocationStateCode == NeighborhoodDeliveryMapStates.Closed || value.RouteStateCode == NeighborhoodDeliveryMapStates.Closed)
            {
                Delivery = null; ClearPrivateCamera(); SelectedRequestId = null;
                SelectedMarkerId = RegionKey is null ? null : "region:" + RegionKey;
                DeliveryError = "배송이 종료되어 위치 표시를 마쳤습니다.";
                Deliveries = Deliveries.Where(item => item.RequestId != id).ToArray(); return;
            }
            if (!includeRoute && value.RouteStateCode == NeighborhoodDeliveryMapStates.NotRequested && previous?.RequestId == id
                && !string.IsNullOrEmpty(value.RouteRevision) && previous.RouteRevision == value.RouteRevision
                && SamePoint(value.Pickup, previous.Pickup) && SamePoint(value.Dropoff, previous.Dropoff)
                && (previous.PlannedRoute?.FromPointRoleCode != "Driver" || IsRecentDriverLocation(value)))
            { value.PlannedRoute = previous.PlannedRoute; value.RouteStateCode = previous.RouteStateCode; value.RouteNotice = previous.RouteNotice; }
            Delivery = value;
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!PrivateCurrent(read, owner, _deliveryRequests)) return;
            Delivery = null; ClearPrivateCamera();
            if (ex is SsalddelApiException { StatusCode: 401 }) { RequiresLogin = true; ClearPrivate(); }
            DeliveryError = "배송 위치를 확인하지 못했습니다. 다시 조회해 주세요.";
        }
        finally { if (PrivateCurrent(read, owner, _deliveryRequests)) { DeliveryLoading = false; Changed(true); } else if (!_disposed) Changed(true); }
    }
    public NeighborhoodMapRenderState RenderState => NeighborhoodMapSceneBuilder.Build(_revision,
        Regions, _map, PublicDataMarkers, StorageMarkers, Deliveries, Delivery, HasLayer,
        !_privateReadSuspended && IsAuthenticated && _owner == CurrentOwner, HasRecentDriverLocation, SelectedRequestId, SelectedMarkerId,
        SelectedRegion, SelectedPublicData, _privateViewport && (!IsAuthenticated || _owner != CurrentOwner) ? null : _viewport, PreserveViewport);
    public bool HasRecentDriverLocation => IsAuthenticated && _owner == CurrentOwner && Delivery is { } delivery && IsRecentDriverLocation(delivery);
    private bool IsRecentDriverLocation(NeighborhoodDeliveryMapResponse delivery) => delivery is { DriverLocationStateCode: NeighborhoodDeliveryMapStates.Available, DriverLocation: { } driver }
        && driver.MeasuredAtUtc <= UtcNow && driver.ReceivedAtUtc <= UtcNow
        && UtcNow - driver.MeasuredAtUtc <= TimeSpan.FromMinutes(10) && UtcNow - driver.ReceivedAtUtc <= TimeSpan.FromMinutes(10)
        && ValidPoint((double)driver.Latitude, (double)driver.Longitude);
    public async Task LoadPublicDataAsync()
    {
        if (_disposed || !HasLayer(NeighborhoodMapNavigation.PublicData)) return;
        var read = _publicDataRequests.Begin(_lifetime.Token); PublicDataLoading = true; PublicDataError = null; Changed();
        try
        {
            var result = await client.PublicDataAsync(read.Token);
            if (_disposed || !_publicDataRequests.IsCurrent(read)) return;
            if (result is null) { PublicDataError = "공공자료는 별도 화면에서 확인할 수 있습니다."; return; }
            PublicDataMarkers = result.Items.Where(item => item.CountryCode == "KR" && item.RegionTypeCode == RegionalAgriculturalMapRegionTypeCodes.StateProvince
                && ValidPoint((double)item.Latitude, (double)item.Longitude) && !string.IsNullOrWhiteSpace(item.AnchorSourceKey)
                && !string.IsNullOrWhiteSpace(item.AnchorSourceVintage) && Uri.TryCreate(item.AnchorSourceUrl, UriKind.Absolute, out var source) && source.Scheme == "https").ToArray();
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && _publicDataRequests.IsCurrent(read)) { PublicDataMarkers = []; PublicDataError = "공공자료를 불러오지 못했습니다. 다시 시도하거나 별도 화면을 이용해 주세요."; } }
        finally { if (!_disposed && _publicDataRequests.IsCurrent(read)) { PublicDataLoading = false; Changed(true); } }
    }
    public async Task LoadStorageAsync()
    {
        if (_disposed || !HasLayer(NeighborhoodMapNavigation.Storage)) return;
        var read = _storageRequests.Begin(_lifetime.Token); StorageLoading = true; StorageError = null; Changed();
        try
        {
            var result = await client.StorageAsync(read.Token);
            if (_disposed || !_storageRequests.IsCurrent(read) || !HasLayer(NeighborhoodMapNavigation.Storage)) return;
            if (result is null) { StorageError = "보관공간은 별도 목록에서 확인할 수 있습니다."; StorageMarkers = []; return; }
            var known = Regions.ToDictionary(region => region.RegionKey, StringComparer.Ordinal);
            StorageMarkers = result.Items.Where(item => known.ContainsKey(item.Region.RegionKey) && item.SpaceCount > 0)
                .GroupBy(item => item.Region.RegionKey).Select(group => new NeighborhoodStorageMapMarkerDto
                { Region = known[group.Key], SpaceCount = group.Sum(item => item.SpaceCount) }).ToArray();
        }
        catch (OperationCanceledException) when (read.Token.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && _storageRequests.IsCurrent(read)) { StorageMarkers = []; StorageError = "보관공간을 불러오지 못했습니다. 다시 시도해 주세요."; } }
        finally { if (!_disposed && _storageRequests.IsCurrent(read)) { StorageLoading = false; Changed(true); } }
    }
    private async Task SaveAsync()
    {
        if (_disposed) return;
        var owner = _owner; PreferenceError = null;
        try
        {
            await _preferenceWrites.WaitAsync(_lifetime.Token);
            try { if (!_disposed && owner == _owner) await preferences.SaveAsync(owner, new(SelectedLayers, ViewMode, RegionKey), _lifetime.Token); }
            finally { _preferenceWrites.Release(); }
        }
        catch (Exception) { if (!_disposed && _owner == owner) PreferenceError = "화면 설정을 저장하지 못했습니다. 현재 선택은 유지됩니다."; }
    }
    private bool PublicCurrent(NeighborhoodMapLayerRead read) => !_disposed && _publicRequests.IsCurrent(read);
    private bool PrivateCurrent(NeighborhoodMapLayerRead read, string? owner, NeighborhoodMapLayerRequest source)
        => !_disposed && source.IsCurrent(read) && owner == _owner && owner == CurrentOwner && IsAuthenticated;
    private static bool SamePoint(NeighborhoodDeliveryMapPoint? a, NeighborhoodDeliveryMapPoint? b)
        => a is not null && b is not null && a.Latitude == b.Latitude && a.Longitude == b.Longitude;
    private static bool ValidPoint(double latitude, double longitude) => double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static bool IsPublicRegion(NeighborhoodPublicRegionDto region) => region.PrecisionCode == "neighborhood" && !string.IsNullOrWhiteSpace(region.RegionKey) && ValidPoint(region.Latitude, region.Longitude);
    public void SetViewport(NeighborhoodMapViewport viewport)
    {
        if (_disposed || _privateReadSuspended || !ValidPoint(viewport.Latitude, viewport.Longitude) || !double.IsFinite(viewport.Zoom)) return;
        _viewport = viewport with { Zoom = Math.Clamp(viewport.Zoom, 1, 22) };
        _privateViewport = IsAuthenticated && HasLayer(NeighborhoodMapNavigation.Mine)
            && (SelectedRequestId is not null || Deliveries.Any(item => item.Pickup is not null || item.Dropoff is not null));
        PreserveViewport = true;
        Remember();
    }
    public void InvalidateMap() { if (!_disposed) { _privateReadSuspended = false; Changed(true); } }
    public void SuspendPrivate()
    {
        if (_disposed) return;
        _privateReadSuspended = true;
        _mineRequests.Invalidate(); _deliveryRequests.Invalidate();
        Deliveries = []; Delivery = null; MineLoading = DeliveryLoading = false;
        MineError = DeliveryError = null;
        ClearPrivateCamera();
        // Stable ID와 협업 문맥만 유지합니다. 재개할 때 본인 목록·선택 원장을 새로 조회해야 합니다.
        Changed(true);
    }
    public void SelectPanel(string? kind, string? target = null, string? related = null)
    {
        if (_disposed) return;
        var nextKind = NeighborhoodMapNavigation.PanelKind(kind);
        var nextTarget = nextKind is "post" or "work" or "space" or "delivery" ? NeighborhoodMapNavigation.PanelTarget(target) : null;
        var nextRelated = nextKind == "space" ? NeighborhoodMapNavigation.PanelTarget(related) : null;
        if (PanelKind == nextKind && PanelTarget == nextTarget && PanelRelated == nextRelated) return;
        if (nextKind != "delivery" || nextTarget != SelectedRequestId) ClearSelection();
        PanelKind = nextKind; PanelTarget = nextTarget; PanelRelated = nextRelated;
        if (nextKind is "work" or "delivery") PanelScope = nextKind;
        PreserveViewport = true;
        Changed();
    }
    public void SetPanelScope(string scope)
    {
        if (_disposed || scope is not ("work" or "delivery" or "space")) return;
        PanelScope = scope;
        Changed();
    }
    public void RecordListContext(string kind, int page, string? scope = null)
    {
        if (_disposed) return;
        page = Math.Clamp(page, 1, 10000);
        switch (kind)
        {
            case "work": WorkListPage = page; WorkListScope = scope == "undertaken" ? "undertaken" : "requested"; break;
            case "delivery": DeliveryListPage = page; break;
            case "space": SpaceListPage = page; SpaceListMine = scope == "mine"; break;
            default: return;
        }
        // 목록 callback 자체는 새 렌더를 요구하지 않습니다.
        Remember();
    }
    private void ClearPrivateCamera() { if (_privateViewport) { _viewport = null; _privateViewport = false; PreserveViewport = false; } }
    private void Remember()
    {
        if (!_initialized || _disposed || _owner != CurrentOwner) return;
        _workspace.Save(_owner, new(SelectedLayers, ViewMode, RegionKey, Page, MinePage, SelectedMarkerId,
            SelectedRequestId, _viewport, _privateViewport, PanelKind, PanelTarget, PanelScope, PanelRelated,
            WorkListPage, WorkListScope, DeliveryListPage, SpaceListPage, SpaceListMine));
    }
    private void Changed(bool sceneChanged = false)
    {
        if (_disposed) return;
        if (sceneChanged) ++_revision;
        Remember();
        OnPropertyChanged(string.Empty);
    }
    public void Dispose()
    {
        if (_disposed) return;
        Remember();
        _disposed = true;
        ClearPrivate(false);
        foreach (var source in new[] { _publicRequests, _mapRequests, _postsRequests, _mineRequests, _deliveryRequests, _publicDataRequests, _storageRequests }) source.Dispose();
        _lifetime.Cancel(); _lifetime.Dispose();
    }
}
