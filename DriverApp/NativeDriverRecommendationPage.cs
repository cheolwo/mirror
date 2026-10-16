using DriverApp.Controls;
using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;

namespace DriverApp;

/// <summary>선택한 추천 한 건의 예상 경로와 조건을 검토합니다. 현재 수행 운송은 변경하지 않습니다.</summary>
public sealed class NativeDriverRecommendationPage : ContentPage
{
    public const string PageKey = "driver-recommendation-map";
    private const string CoordinateWarning = "이 추천의 위치를 지도에서 확인하지 못했습니다. 카드의 주소를 확인해 주세요.";
    private readonly IDriverSampleDataService _samples;
    private readonly IDriverHomeMapService _maps;
    private readonly IDriverRecommendationDecisionService _decisions;
    private readonly IDriverRecommendationNotificationService _notifications;
    private readonly DriverHomeRoutePlanningService _routes;
    private readonly DriverNativeHomeWorkspaceState _workspace;
    private readonly 기사위치표시설정Service _preferences;
    private readonly DriverRecommendationSelectionState _selection = new();
    private readonly DriverNativeMapView _map = new() { HighlightSelectedRecommendation = true };
    private readonly CarouselView _cards = new()
    {
        Loop = false, HeightRequest = 116, IsBounceEnabled = false,
        PeekAreaInsets = new Thickness(40, 0),
        ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal)
        {
            ItemSpacing = 12, SnapPointsType = SnapPointsType.MandatorySingle, SnapPointsAlignment = SnapPointsAlignment.Center
        }
    };
    private readonly List<WeakReference<RecommendationCardSurface>> _cardSurfaces = [];
    private readonly Label _position = new()
    {
        FontSize = 13, FontAutoScalingEnabled = false, TextColor = Color.FromArgb("#586B78"),
        HorizontalTextAlignment = TextAlignment.End
    };
    private readonly Label _status = new() { FontSize = 14, FontAutoScalingEnabled = false, TextColor = Color.FromArgb("#92400E"), LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _back = new() { Text = "운행 지도로", HeightRequest = 48, FontSize = 14, FontAutoScalingEnabled = false };
    private readonly Button _retry = new() { Text = "현재 상태 확인", HeightRequest = 48, FontSize = 14, FontAutoScalingEnabled = false };
    private IReadOnlyList<DriverMapMarkerItem> _projectedMarkers = [];
    private string? _lastFramedCoordinates;
    private Window? _window;
    private IDispatcherTimer? _timer;
    private bool _visible, _foreground, _refreshing, _busy, _prompting, _synchronizing, _needsConfirmation;
    private long _generation;
    private DateTime _nextRefreshAtUtc;
    private DriverRecommendationSelectionKey? _initialSelection;

    public NativeDriverRecommendationPage(IDriverSampleDataService samples, IDriverHomeMapService maps,
        IDriverRecommendationDecisionService decisions, IDriverRecommendationNotificationService notifications,
        DriverHomeRoutePlanningService routes, DriverNativeHomeWorkspaceState workspace, 기사위치표시설정Service preferences)
    {
        _samples = samples; _maps = maps; _decisions = decisions; _notifications = notifications;
        _routes = routes; _workspace = workspace; _preferences = preferences;
        Title = "배차 추천";
        NavigationPage.SetHasNavigationBar(this, false);
        _cards.ItemTemplate = new DataTemplate(BuildCard);
        _cards.CurrentItemChanged += (_, args) =>
        {
            if (_synchronizing || _busy || _prompting || args.CurrentItem is not DriverRequestItem item) return;
            if (_selection.Select(item.의뢰Id, item.추천라운드)) ShowSelection();
        };
        _cards.SizeChanged += (_, _) => QueueVisibleReceipt();
        _back.Clicked += async (_, _) => { if (!_busy && !_prompting) await Navigation.PopAsync(); };
        _retry.Clicked += async (_, _) => await RefreshAsync();
        var header = new Grid { Padding = 12, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = GridLength.Star } }, BackgroundColor = Colors.White };
        header.Add(_back, 0, 0);
        header.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center,
            Children = { new Label { Text = "배차 추천", FontSize = 20, FontAutoScalingEnabled = false, HorizontalTextAlignment = TextAlignment.End }, _position } }, 1, 0);
        _status.Margin = new Thickness(16, 0); _status.Padding = 8; _status.BackgroundColor = Colors.White;
        _retry.Margin = new Thickness(16, 0);
        var bottom = new VerticalStackLayout
        {
            Spacing = 6, Margin = new Thickness(0, 0, 0, 12), BackgroundColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.End, Children = { _status, _retry, _cards }
        };
        var root = new Grid(); root.Add(_map); root.Add(header); header.VerticalOptions = LayoutOptions.Start; root.Add(bottom);
        Content = root; // 지도는 전체 영역을 유지하며 카드 높이로 SDK 크기·여백을 조정하지 않습니다.
    }

    public void Prefer(string requestId, int round) => _initialSelection = new(requestId, round);

    protected override async void OnAppearing()
    {
        base.OnAppearing(); _visible = _foreground = true; ++_generation;
        _workspace.Invalidated += OnInvalidated;
        _preferences.Changed += OnPreferencesChanged;
        _window = Window;
        if (_window is not null)
        {
            _window.Activated += OnActivated; _window.Resumed += OnActivated;
            _window.Deactivated += OnDeactivated; _window.Stopped += OnDeactivated;
        }
        ApplyPreferences(); StartTimer(); await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        _visible = _foreground = false; ++_generation; StopTimer();
        _map.IsDirectionTrackingActive = false;
        _workspace.Invalidated -= OnInvalidated; _preferences.Changed -= OnPreferencesChanged;
        if (_window is not null)
        {
            _window.Activated -= OnActivated; _window.Resumed -= OnActivated;
            _window.Deactivated -= OnDeactivated; _window.Stopped -= OnDeactivated; _window = null;
        }
        _workspace.Pause(); // 뒤로 가도 응답이 불명확한 수락의 ID·차수는 같은 기사 작업공간에 남깁니다.
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_busy || _prompting) return true;
        return base.OnBackButtonPressed();
    }
    private async void OnActivated(object? sender, EventArgs e)
    { if (!_visible || _foreground) return; _foreground = true; ++_generation; ApplyPreferences(); StartTimer(); await RefreshAsync(); }
    private void OnDeactivated(object? sender, EventArgs e)
    { if (!_visible || !_foreground) return; _foreground = false; ++_generation; StopTimer(); _map.IsDirectionTrackingActive = false; _workspace.Pause(); UpdateActions(); }
    private void OnPreferencesChanged() => Dispatcher.Dispatch(ApplyPreferences);
    private void ApplyPreferences() { _map.ShowDirectionArrow = _preferences.방향화살표표시; _map.IsDirectionTrackingActive = _visible && _foreground; }
    private void OnInvalidated() => Dispatcher.Dispatch(() =>
    {
        ++_generation; _selection.Refresh([], DateTime.UtcNow);
        _map.CurrentLocation = null; ClearMapProjection();
        _status.Text = _workspace.ErrorMessage; SynchronizeCards(); UpdateActions();
    });

    private void StartTimer()
    {
        if (_timer is not null) return;
        _timer = Dispatcher.CreateTimer(); _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick; _timer.Start();
    }
    private void StopTimer() { if (_timer is null) return; _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (!_visible || !_foreground) return;
        if (_projectedMarkers.Any(marker => !_selection.Items.Any(item => item.의뢰Id == marker.RequestId
            && DriverRecommendationSelectionState.IsLive(item, DateTime.UtcNow)))) RefreshMapProjection();
        UpdateActions();
        if (!_busy && !_refreshing && DateTime.UtcNow >= _nextRefreshAtUtc) await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!_visible || !_foreground || _busy || _prompting || _refreshing) return;
        _refreshing = true; var generation = _generation; UpdateActions();
        _status.Text = "추천 확인 중";
        try
        {
            if (!_workspace.IsActive) await _workspace.ActivateAsync(); else await _workspace.RefreshAsync();
            if (!IsCurrent(generation)) return;
            if (!_workspace.IsReady || !_samples.ReadState.Recommendations.IsCurrent)
            {
                _status.Text = !_workspace.IsReady ? _workspace.ErrorMessage : "추천 상태를 확인하지 못했습니다. 현재 상태를 다시 확인해 주세요.";
                ClearMapProjection(); return;
            }
            _selection.Refresh(_samples.추천의뢰목록, DateTime.UtcNow);
            _needsConfirmation = false;
            if (_initialSelection is { } initial) { _selection.Select(initial.RequestId, initial.Round); _initialSelection = null; }
            _map.CurrentLocation = _samples.기사현재위치;
            _status.Text = _workspace.UnconfirmedAcceptanceRequestId is not null
                ? "수락 결과를 확인하고 있습니다. 새로 수락하지 않고 현재 상태를 다시 확인해 주세요."
                : _selection.Items.Count == 0 ? "현재 확인할 수 있는 추천이 없습니다. 운행 지도로 돌아가거나 다시 확인해 주세요."
                : string.Empty;
            SynchronizeCards();
        }
        finally
        {
            _refreshing = false; _nextRefreshAtUtc = DateTime.UtcNow.AddSeconds(30);
            if (_visible && _foreground)
            {
                UpdateActions(); QueueVisibleReceipt();
                if (generation != _generation) Dispatcher.Dispatch(async () => await RefreshAsync());
            }
        }
    }

    private void SynchronizeCards()
    {
        _synchronizing = true;
        _cards.ItemsSource = _selection.Items;
        if (_selection.SelectedIndex >= 0)
        { _cards.CurrentItem = _selection.Selected; _cards.Position = _selection.SelectedIndex; }
        _synchronizing = false; _cards.IsVisible = _selection.Items.Count > 0;
        ShowSelection();
    }

    private void ShowSelection()
    {
        var item = _selection.Selected;
        _position.Text = item is null ? "추천 0건" : $"{_selection.SelectedIndex + 1} / {_selection.Items.Count} · 예상 경로";
        if (_status.Text == CoordinateWarning) _status.Text = string.Empty;
        RefreshMapProjection();
        UpdateActions(); QueueVisibleReceipt();
    }

    private void RefreshMapProjection()
    {
        _map.RouteOverlays = [];
        if (!_workspace.IsReady || !_samples.ReadState.Recommendations.IsCurrent)
        {
            ClearMapProjection();
            return;
        }
        var markers = new List<DriverMapMarkerItem>();
        for (var index = 0; index < _selection.Items.Count; index++)
        {
            var item = _selection.Items[index];
            if (!DriverRecommendationSelectionState.IsLive(item, DateTime.UtcNow)) continue;
            var pickup = CoordinatesValid((double)(item.픽업_위도 ?? 0m), (double)(item.픽업_경도 ?? 0m));
            var dropoff = CoordinatesValid((double)(item.하차_위도 ?? 0m), (double)(item.하차_경도 ?? 0m));
            if (!pickup && !dropoff) continue;
            // DTO의 0은 미확인 점의 sentinel이며 handler가 그 점을 생략합니다. 다른 유효한 점은 보존합니다.
            markers.Add(new(item.의뢰Id,
                pickup ? (double)item.픽업_위도!.Value : 0d, pickup ? (double)item.픽업_경도!.Value : 0d,
                dropoff ? (double)item.하차_위도!.Value : 0d, dropoff ? (double)item.하차_경도!.Value : 0d,
                item.화물종류, item.요약설명, item.픽업지, item.하차지, $"{index + 1} 픽업", $"{index + 1} 전달"));
        }
        if (!_projectedMarkers.SequenceEqual(markers)) { _map.Markers = markers; _projectedMarkers = markers; }
        var selected = _selection.Selected;
        var selectedLive = selected is not null && DriverRecommendationSelectionState.IsLive(selected, DateTime.UtcNow);
        _map.SelectedRecommendationRequestId = selectedLive ? selected!.의뢰Id : null;
        var marker = selectedLive ? markers.FirstOrDefault(x => x.RequestId == selected!.의뢰Id) : null;
        if (marker is not null)
        {
            var location = DriverNativeLocationPolicy.ForCurrentRoute(_samples.기사현재위치, DateTime.UtcNow);
            _map.RouteOverlays = _routes.BuildRecommendationReviewRouteOverlays(location, _workspace.CurrentTransport, marker);
        }
        else if (selectedLive && string.IsNullOrWhiteSpace(_status.Text)) _status.Text = CoordinateWarning;
        FrameMarkersIfChanged(markers);
    }

    private void ClearMapProjection()
    {
        _map.Markers = []; _projectedMarkers = []; _map.SelectedRecommendationRequestId = null; _map.RouteOverlays = [];
    }

    private void FrameMarkersIfChanged(IReadOnlyList<DriverMapMarkerItem> markers)
    {
        var points = markers.SelectMany(x => new[] { (x.PickupLatitude, x.PickupLongitude), (x.DropoffLatitude, x.DropoffLongitude) })
            .Where(x => CoordinatesValid(x.Item1, x.Item2)).Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToArray();
        var fingerprint = string.Join(";", points.Select(x => FormattableString.Invariant($"{x.Item1:R},{x.Item2:R}")));
        if (fingerprint == _lastFramedCoordinates) return;
        _lastFramedCoordinates = fingerprint;
        if (points.Length == 0) return;
        var span = Math.Max(points.Max(x => x.Item1) - points.Min(x => x.Item1), points.Max(x => x.Item2) - points.Min(x => x.Item2));
        _map.CenterLatitude = (points.Min(x => x.Item1) + points.Max(x => x.Item1)) / 2d;
        _map.CenterLongitude = (points.Min(x => x.Item2) + points.Max(x => x.Item2)) / 2d;
        _map.Zoom = span switch
        {
            <= 0.01d => 14d, <= 0.03d => 13d, <= 0.08d => 12d, <= 0.18d => 11d,
            <= 0.35d => 10d, <= 0.7d => 9d, <= 1.5d => 8d, <= 3d => 7d, _ => 6d
        };
    }

    private static bool CoordinatesValid(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 and not 0
            && longitude is >= -180 and <= 180 and not 0;

    private static bool HasWarnings(DriverRequestItem item) => item.경고목록.Count > 0 || item.확인필요경고코드.Count > 0;

    private View BuildCard()
    {
        var fare = Text(string.Empty, 18, true);
        var fareCaption = Text("배달료", 11);
        fareCaption.TextColor = Color.FromArgb("#586B78");
        var pickup = Text(string.Empty, 13);
        var dropoff = Text(string.Empty, 13);
        var summary = new Grid { ColumnSpacing = 8, VerticalOptions = LayoutOptions.Center,
            ColumnDefinitions = { new() { Width = new GridLength(104) }, new() { Width = GridLength.Star } } };
        summary.Add(new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center,
            Children = { fareCaption, fare } }, 0, 0);
        summary.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center,
            Children = { pickup, dropoff } }, 1, 0);
        var reject = new Button { Text = "거절", HeightRequest = 48, FontSize = 14, FontAutoScalingEnabled = false,
            BackgroundColor = Color.FromArgb("#2563EB"), TextColor = Colors.White };
        var accept = new Button { Text = "수락", HeightRequest = 48, FontSize = 14, FontAutoScalingEnabled = false,
            BackgroundColor = Color.FromArgb("#FB923C"), TextColor = Color.FromArgb("#172B36") };
        var actions = new Grid { ColumnSpacing = 8,
            ColumnDefinitions = { new() { Width = GridLength.Star }, new() { Width = GridLength.Star } } };
        actions.Add(reject, 0, 0); actions.Add(accept, 1, 0);
        var body = new Grid { RowSpacing = 4,
            RowDefinitions = { new() { Height = GridLength.Star }, new() { Height = GridLength.Auto } } };
        body.Add(new ScrollView { Content = summary, VerticalScrollBarVisibility = ScrollBarVisibility.Default }, 0, 0);
        body.Add(actions, 0, 1);
        var primary = new BoxView { WidthRequest = 4 };
        var secondary = new BoxView { WidthRequest = 4 };
        var layout = new Grid { ColumnSpacing = 4,
            ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = GridLength.Auto }, new() { Width = GridLength.Star } } };
        layout.Add(primary, 0, 0); layout.Add(secondary, 1, 0); layout.Add(body, 2, 0);
        var card = new Border
        {
            Margin = new Thickness(0, 2), Padding = new Thickness(8, 4), BackgroundColor = Color.FromArgb("#F8FAFC"),
            Stroke = Color.FromArgb("#DCE5E9"), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) }, Content = layout
        };
        var surface = new RecommendationCardSurface(card, reject, accept);
        _cardSurfaces.Add(new(surface));
        reject.Clicked += async (_, _) => { if (surface.Key is { } key) await DecideAsync(key, accept: false); };
        accept.Clicked += async (_, _) => { if (surface.Key is { } key) await DecideAsync(key, accept: true); };
        card.BindingContextChanged += (_, _) =>
        {
            surface.Key = null;
            if (card.BindingContext is not DriverRequestItem item) { UpdateActions(); return; }
            surface.Key = DriverRecommendationSelectionState.Key(item);
            fare.Text = item.OfferPricing.HasEvidence ? item.OfferPricing.TotalText : item.운임표시;
            pickup.Text = $"픽업지  {item.픽업지}"; dropoff.Text = $"전달지  {item.하차지}";
            primary.Color = Color.FromArgb(item.OfferVisual.PrimaryColor);
            secondary.IsVisible = item.OfferVisual.IsCombined;
            secondary.Color = Color.FromArgb(item.OfferVisual.SecondaryColor ?? item.OfferVisual.PrimaryColor);
            UpdateActions();
        };
        return card;
    }

    private sealed class RecommendationCardSurface(Border root, Button reject, Button accept)
    {
        public DriverRecommendationSelectionKey? Key { get; set; }
        public Border Root { get; } = root;
        public Button Reject { get; } = reject;
        public Button Accept { get; } = accept;
    }

    private static Label Text(string text, double size = 16, bool bold = false)
        => new() { Text = text, FontSize = size, FontAutoScalingEnabled = false, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            TextColor = Color.FromArgb("#172B36"), LineBreakMode = LineBreakMode.WordWrap };
    private static string WarningFingerprint(DriverRequestItem item)
        => string.Join("\n", item.경고목록) + "\n" + string.Join("\n", item.확인필요경고코드);
    private bool IsCurrent(long generation) => _visible && _foreground && generation == _generation;
    private bool CanRespondTo(DriverRecommendationSelectionKey key) => _visible && _foreground && !_refreshing && !_needsConfirmation
        && _workspace.CanRespondToRecommendation && _selection.SelectedKey == key && _selection.FindLive(key, DateTime.UtcNow) is not null;
    private bool CanAct() => !_busy && !_prompting && _selection.SelectedKey is { } key && CanRespondTo(key);
    private void UpdateActions()
    {
        var now = DateTime.UtcNow;
        var selectedLive = _selection.Selected is { } selected && DriverRecommendationSelectionState.IsLive(selected, now);
        var canAct = CanAct();
        _cards.IsEnabled = !_busy && !_prompting; _cards.IsSwipeEnabled = !_busy && !_prompting;
        _cardSurfaces.RemoveAll(x => !x.TryGetTarget(out _));
        foreach (var reference in _cardSurfaces)
        {
            if (!reference.TryGetTarget(out var surface)) continue;
            var isSelectedSurface = surface.Key is not null && surface.Key == _selection.SelectedKey;
            surface.Root.Stroke = Color.FromArgb(isSelectedSurface ? "#64748B" : "#DCE5E9");
            surface.Root.StrokeThickness = isSelectedSurface ? 2 : 1;
            surface.Accept.IsEnabled = surface.Reject.IsEnabled = isSelectedSurface && canAct;
        }
        _back.IsEnabled = !_busy && !_prompting; _retry.IsEnabled = !_busy && !_prompting && !_refreshing && _foreground;
        if (_selection.Selected is not null && !selectedLive)
        {
            if (_workspace.IsReady && _samples.ReadState.Recommendations.IsCurrent)
                _status.Text = "추천 시간이 지났습니다. 현재 상태를 확인해 주세요.";
            _map.SelectedRecommendationRequestId = null;
            if (_map.RouteOverlays.Count > 0) _map.RouteOverlays = [];
        }
        _retry.IsVisible = _needsConfirmation || !_workspace.IsReady || !_samples.ReadState.Recommendations.IsCurrent
            || _workspace.UnconfirmedAcceptanceRequestId is not null
            || _selection.Selected is { } offer && !DriverRecommendationSelectionState.IsLive(offer, DateTime.UtcNow);
        _status.IsVisible = !string.IsNullOrWhiteSpace(_status.Text);
    }

    private void QueueVisibleReceipt() => Dispatcher.Dispatch(async () =>
    {
        if (!CanAct() || _cards.Width <= 0 || _cards.Height <= 0 || _selection.Selected is not { } item
            || _cards.CurrentItem is not DriverRequestItem visible || DriverRecommendationSelectionState.Key(visible) != DriverRecommendationSelectionState.Key(item)) return;
        await _workspace.AcknowledgePresentedAsync(item.의뢰Id, item.추천라운드);
    });

    private async Task DecideAsync(DriverRecommendationSelectionKey key, bool accept)
    {
        if (!CanAct() || key != _selection.SelectedKey || _selection.FindLive(key, DateTime.UtcNow) is not { } selected
            || _cards.CurrentItem is not DriverRequestItem visible || DriverRecommendationSelectionState.Key(visible) != key) return;
        // 확인 창을 띄우기 전에 잠가 같은 카드의 연속 탭과 다른 카드의 응답을 함께 막습니다.
        _prompting = true; UpdateActions();
        var generation = _generation; var workspaceGeneration = _workspace.Generation;
        string? acknowledged = null;
        string? reason = null;
        var acceptanceSubmitted = false;
        try
        {
            if (accept && HasWarnings(selected))
            {
                var warning = selected.경고목록.Count > 0 ? string.Join("\n", selected.경고목록)
                    : "이 배차에 연결된 차량·화물 주의사항 확인이 필요합니다.";
                var fingerprint = WarningFingerprint(selected);
                if (!await DisplayAlertAsync("주의사항 확인", warning + "\n\n확인 후 이 추천을 수락할까요?", "확인 후 수락", "돌아가기")) return;
                acknowledged = fingerprint; // 이번 창의 한 건·한 차수에만 유효하며 다음 수락에 재사용하지 않습니다.
            }
            else if (!accept)
            {
                reason = await DisplayPromptAsync("추천 거절", "거절 사유를 입력해 주세요.", "거절", "돌아가기", initialValue: "동선이 맞지 않음");
                if (reason is null) return;
            }
            if (!IsCurrent(generation) || workspaceGeneration != _workspace.Generation || !CanRespondTo(key)
                || _cards.CurrentItem is not DriverRequestItem current || DriverRecommendationSelectionState.Key(current) != key) return;
            _busy = true; _prompting = false; UpdateActions();
            await _workspace.RefreshAsync();
            if (!IsCurrent(generation) || workspaceGeneration != _workspace.Generation || _selection.SelectedKey != key) return;
            if (!_workspace.CanRespondToRecommendation)
            {
                _needsConfirmation = true;
                _status.Text = !_workspace.IsReady ? _workspace.ErrorMessage : "현재 배차를 처리할 수 없습니다. 현재 상태를 확인해 주세요.";
                ClearMapProjection(); return;
            }
            var request = _samples.추천의뢰목록.FirstOrDefault(x => DriverRecommendationSelectionState.Key(x) == key && DriverRecommendationSelectionState.IsLive(x, DateTime.UtcNow));
            if (request is null) { _needsConfirmation = true; _status.Text = "추천이 변경됐습니다. 현재 상태를 확인해 주세요."; return; }
            if (accept && HasWarnings(request) && acknowledged != WarningFingerprint(request))
            { _needsConfirmation = true; _status.Text = "주의사항이 변경됐습니다. 다시 확인해 주세요."; return; }
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(_workspace.LifetimeToken);
            bounded.CancelAfter(TimeSpan.FromSeconds(10));
            if (accept)
            {
                _workspace.KeepUnconfirmedAcceptance(key.RequestId, key.Round);
                acceptanceSubmitted = true;
                await _decisions.AcceptAsync(request, HasWarnings(request) ? request.확인필요경고코드 : [], bounded.Token);
            }
            else await _decisions.RejectAsync(request, reason!, bounded.Token);
            if (!IsCurrent(generation) || workspaceGeneration != _workspace.Generation) return;
            if (accept) _workspace.ConfirmAcceptanceResponse(key.RequestId);
            _notifications.MarkHandled(key.RequestId);
            await Navigation.PopAsync();
        }
        catch (Exception error)
        {
            if (!IsCurrent(generation) || workspaceGeneration != _workspace.Generation) return;
            _needsConfirmation = true;
            if (acceptanceSubmitted) _workspace.ObserveAcceptanceFailure(key.RequestId, key.Round, error);
            _status.Text = accept && _workspace.UnconfirmedAcceptanceRequestId is not null
                ? "수락 결과가 확인되지 않았습니다. 다시 수락하지 말고 현재 상태를 확인해 주세요." : error.Message;
        }
        finally
        {
            _busy = false; _prompting = false;
            if (_visible && _foreground)
            {
                UpdateActions();
                if (generation != _generation) Dispatcher.Dispatch(async () => await RefreshAsync());
            }
        }
    }
}
