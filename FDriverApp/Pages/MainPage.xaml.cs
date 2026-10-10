using System.ComponentModel;
using FDriverApp.Models;
using FDriverApp.PageModels;
using Ssalddel.Contracts.Common.Drivers;

namespace FDriverApp.Pages
{
    public partial class MainPage : ContentPage, IQueryAttributable
    {
        private string? _entryFocus;
        private bool _pageActive;
        private int _appearanceRevision;
        private int _navigationRevision;
        private int? _restoringScrollRevision;
        private bool _foodLayoutUpdateQueued;
        private bool _dispatchReceptionUpdating;
        private bool _profileNavigationInProgress;
        private readonly IFDriverCompletedDeliveryNavigator _completedDeliveryNavigator;
        private readonly IFDriverProtectionSupportNavigator _supportNavigator;

        public MainPage(MainPageModel model, IFDriverCompletedDeliveryNavigator completedDeliveryNavigator,
            IFDriverProtectionSupportNavigator supportNavigator)
        {
            InitializeComponent();
            BindingContext = model;
            _completedDeliveryNavigator = completedDeliveryNavigator;
            _supportNavigator = supportNavigator;
        }

        private void OnFoodLayoutSizeChanged(object? sender, EventArgs args) => QueueFoodLayoutUpdate();

        private void OnFoodMapPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(global::FDriverApp.Controls.FDriverNativeMapView.IsMapReady))
                QueueFoodLayoutUpdate();
        }

        private void QueueFoodLayoutUpdate()
        {
            if (_foodLayoutUpdateQueued) return;
            _foodLayoutUpdateQueued = true;
            Dispatcher.Dispatch(() =>
            {
                _foodLayoutUpdateQueued = false;
                if (BindingContext is not MainPageModel model || WorkspaceAreas.Height <= 0 || WorkspaceAreas.Width <= 0) return;
                var footerHeight = DeliveryPrimaryFooter.IsVisible
                    ? Math.Max(48, DeliveryPrimaryFooter.Height) : 0;
                // Only overlay layout changes here. The map keeps the same full viewport
                // and camera while the task card opens or closes.
                FoodMapTools.Margin = new Thickness(8, Math.Max(8, WorkspaceHeader.Height + 8), 8, 8);
                FoodDispatchReceptionControl.Margin = new Thickness(8, Math.Max(8, WorkspaceHeader.Height + 8), 8, 8);
                var height = FDriverFoodPresentationLayout.CardHeight(WorkspaceAreas.Height,
                    DeliveryCardBodyContent.Height, footerHeight, model.IsFoodCardExpanded);
                if (Math.Abs(DeliveryCard.HeightRequest - height) > 0.5)
                    DeliveryCard.HeightRequest = height;
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _pageActive = true;
            var appearanceRevision = ++_appearanceRevision;
            var navigationRevision = _navigationRevision;
            if (BindingContext is MainPageModel model)
            {
                model.PropertyChanged -= OnModelPropertyChanged;
                model.PropertyChanged += OnModelPropertyChanged;
                await model.ActivateWorkspacePageAsync();
                if (!_pageActive || appearanceRevision != _appearanceRevision)
                    return;
                if (model.IsAuthenticated && !model.IsBusy && await _supportNavigator.ResumeAfterLoginAsync()) return;
                if (!_pageActive || appearanceRevision != _appearanceRevision
                    || navigationRevision != _navigationRevision)
                    return;
                var focus = _entryFocus;
                if (model.IsAuthenticated && !model.HasWorkspaceWarning)
                    model.ApplyEntryFocus(focus);
                if (model.IsAuthenticated)
                    model.Navigation.TryApplyFocus(focus);
                if (model.IsAuthenticated && model.Navigation.IsProfile)
                    await model.RefreshProfileSettlementAsync();
                if (model.IsAuthenticated && model.Navigation.IsSettlement)
                {
                    _entryFocus = null;
                    model.Navigation.Select(FDriverWorkspaceSection.Profile);
                    await _completedDeliveryNavigator.OpenListAsync(DateOnly.FromDateTime(model.SelectedSettlementDate));
                    return;
                }
                if (!_pageActive || appearanceRevision != _appearanceRevision
                    || navigationRevision != _navigationRevision)
                    return;
                _entryFocus = null;
                await ScrollToEntryFocusAsync(focus);
            }
        }

        protected override void OnDisappearing()
        {
            RememberCurrentScroll();
            _pageActive = false;
            _appearanceRevision++;
            _navigationRevision++;
            _restoringScrollRevision = null;
            if (BindingContext is MainPageModel model)
            {
                model.PropertyChanged -= OnModelPropertyChanged;
                model.SetWorkspacePageVisible(false);
            }

            base.OnDisappearing();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _entryFocus = query.TryGetValue(FDriverWorkspaceNavigator.FocusQueryKey, out var value)
                ? Uri.UnescapeDataString(Convert.ToString(value) ?? string.Empty)
                : null;
        }

        private async void OnMapMarkerSelected(object? sender, DriverMapMarkerItem marker)
        {
            if (BindingContext is MainPageModel model)
            {
                await model.SelectTicketByIdAsync(marker.RequestId);
            }
        }

        private async void OnProfileClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Profile);

        private async void OnDispatchReceptionToggled(object? sender, ToggledEventArgs args)
        {
            if (_dispatchReceptionUpdating || sender is not Switch toggle
                || BindingContext is not MainPageModel model
                || args.Value == model.IsDispatchReceptionOn) return;

            _dispatchReceptionUpdating = true;
            try
            {
                // A switch gesture requests a change; the server read owns its displayed state.
                toggle.IsToggled = model.IsDispatchReceptionOn;
                if (_pageActive && model.Navigation.IsDelivery && model.CanUseDispatchReceptionSwitch)
                    await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            }
            finally
            {
                toggle.IsToggled = model.IsDispatchReceptionOn;
                _dispatchReceptionUpdating = false;
            }
        }

        private async void OnSettlementClicked(object? sender, EventArgs args)
        {
            if (_pageActive && BindingContext is MainPageModel { IsAuthenticated: true } model)
                await _completedDeliveryNavigator.OpenListAsync(DateOnly.FromDateTime(model.SelectedSettlementDate));
        }

        private async void OnAgreedRatesClicked(object? sender, EventArgs args)
        {
            if (BindingContext is MainPageModel { IsAuthenticated: true } && Shell.Current is { } shell)
                await shell.GoToAsync(AgreedRateTablePage.RouteName);
        }

        private async void OnReturnAreaSettingsClicked(object? sender, EventArgs args)
            => await OpenProfileSettingsAsync(ReturnAreaSettingsPage.RouteName);

        private async void OnDeliveryExperienceSettingsClicked(object? sender, EventArgs args)
            => await OpenProfileSettingsAsync(DeliveryExperienceSettingsPage.RouteName);

        private async void OnMartDeliveriesClicked(object? sender, EventArgs args)
            => await OpenProfileSettingsAsync(살뜰마트배송Page.RouteName);

        private async Task OpenProfileSettingsAsync(string route)
        {
            if (_profileNavigationInProgress || !_pageActive
                || BindingContext is not MainPageModel { IsAuthenticated: true } model
                || !model.Navigation.IsProfile || Shell.Current is not { } shell) return;
            _profileNavigationInProgress = true;
            try { await shell.GoToAsync(route); }
            catch { await DisplayAlertAsync("화면을 열지 못했습니다", "잠시 후 다시 선택해 주세요.", "확인"); }
            finally { _profileNavigationInProgress = false; }
        }

        private async void OnNotificationSettingsClicked(object? sender, EventArgs args)
        {
            if (_pageActive && BindingContext is MainPageModel { IsAuthenticated: true } && Shell.Current is { } shell)
                await shell.GoToAsync(NotificationSettingsPage.RouteName);
        }

        private async void OnDeliveryClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery);

        private async void OnRecommendationsClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery, "dispatch");

        private async void OnCurrentDeliveryClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery, "delivery");
        private async void OnProblemClicked(object? sender, EventArgs args)
        {
            if (BindingContext is MainPageModel { IsAuthenticated: true, IsBusy: false, ActiveDelivery: { HasSupportSource: true } delivery })
                await _supportNavigator.OpenAsync(delivery.OrderNo);
        }

        protected override bool OnBackButtonPressed()
        {
            if (BindingContext is MainPageModel { IsAuthenticated: true } editorModel && editorModel.ExceptionEditor.IsOpen)
            {
                if (!editorModel.IsBusy) editorModel.CloseInterruptionCommand.Execute(null);
                return true;
            }
            if (BindingContext is MainPageModel { IsAuthenticated: true } model
                && model.Navigation.IsAuxiliary)
            {
                RememberCurrentScroll();
                _entryFocus = null;
                if (model.Navigation.TryReturnToDelivery())
                {
                    _ = RestoreSectionScrollAsync(model, null);
                    return true;
                }
            }
            return base.OnBackButtonPressed();
        }

        private async void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_pageActive && args.PropertyName is nameof(MainPageModel.IsAuthenticated) or nameof(MainPageModel.IsBusy)
                && sender is MainPageModel { IsAuthenticated: true, IsBusy: false }
                && await _supportNavigator.ResumeAfterLoginAsync()) return;
            if (args.PropertyName is nameof(MainPageModel.IsFoodCardExpanded)
                or nameof(MainPageModel.HasDeliveryFooter) or nameof(MainPageModel.HasRouteSelection))
                QueueFoodLayoutUpdate();
            if (_pageActive && args.PropertyName == nameof(MainPageModel.CurrentDeliveryPresentationKey)
                && sender is MainPageModel { IsAuthenticated: true, HasActiveWork: true } currentModel
                && currentModel.Navigation.IsDelivery && !currentModel.ExceptionEditor.IsOpen)
            {
                await RestoreSectionScrollAsync(currentModel, "delivery");
                return;
            }
            if (_pageActive && args.PropertyName == nameof(MainPageModel.FoodNotificationFocus)
                && sender is MainPageModel { IsAuthenticated: true } notificationModel)
            {
                var focus = notificationModel.TakeFoodNotificationFocus();
                if (focus is not null)
                    await RestoreSectionScrollAsync(notificationModel, focus);
                return;
            }
            if (args.PropertyName == nameof(MainPageModel.IsAuthenticated)
                && sender is MainPageModel { IsAuthenticated: false })
            {
                _navigationRevision++;
                _restoringScrollRevision = null;
                _entryFocus = null;
            }
        }

        private ScrollView ScrollFor(FDriverWorkspaceSection section) => section switch
        {
            FDriverWorkspaceSection.Profile => ProfileScroll,
            _ => WorkspaceScroll
        };

        private async void OnCurrentDeliveryCardToggleClicked(object? sender, EventArgs args)
        {
            if (!_pageActive || BindingContext is not MainPageModel { IsAuthenticated: true, HasActiveWork: true } model
                || !model.Navigation.IsDelivery) return;
            model.ToggleCurrentDeliveryCardCommand.Execute(null);
            QueueFoodLayoutUpdate();
            var revision = ++_navigationRevision;
            var appearance = _appearanceRevision;
            _restoringScrollRevision = revision;
            try
            {
                await Dispatcher.DispatchAsync(() => { });
                if (_pageActive && model.IsAuthenticated && model.Navigation.IsDelivery
                    && revision == _navigationRevision && appearance == _appearanceRevision)
                    await WorkspaceScroll.ScrollToAsync(ActiveDeliverySection, ScrollToPosition.Start, false);
            }
            finally
            {
                if (_restoringScrollRevision == revision) _restoringScrollRevision = null;
            }
        }

        private void RememberCurrentScroll()
        {
            if (BindingContext is not MainPageModel { IsAuthenticated: true } model)
                return;
            var section = model.Navigation.Selected;
            var scroll = ScrollFor(section);
            if (scroll.IsVisible)
                model.Navigation.RememberScroll(section, scroll.ScrollY);
        }

        private void OnWorkspaceScrolled(object? sender, ScrolledEventArgs args)
        {
            if (!_pageActive || _restoringScrollRevision is not null
                || BindingContext is not MainPageModel { IsAuthenticated: true } model
                || !ReferenceEquals(sender, ScrollFor(model.Navigation.Selected)))
                return;
            model.Navigation.RememberScroll(model.Navigation.Selected, args.ScrollY);
            // A fresh user scroll takes precedence over an unfinished appearance restore.
            _navigationRevision++;
        }

        private async Task ShowSectionAsync(FDriverWorkspaceSection section, string? focus = null)
        {
            if (!_pageActive || BindingContext is not MainPageModel { IsAuthenticated: true } model)
                return;

            if (section == FDriverWorkspaceSection.Settlement)
            {
                await _completedDeliveryNavigator.OpenListAsync(DateOnly.FromDateTime(model.SelectedSettlementDate));
                return;
            }

            RememberCurrentScroll();
            _entryFocus = null;
            model.Navigation.Select(section);
            var navigationRevision = ++_navigationRevision;
            if (section == FDriverWorkspaceSection.Profile)
                await model.RefreshProfileSettlementAsync();
            if (!_pageActive || !model.IsAuthenticated || model.Navigation.Selected != section
                || navigationRevision != _navigationRevision)
                return;
            await RestoreSectionScrollAsync(model, focus);
        }

        private Task RestoreSectionScrollAsync(MainPageModel model, string? focus)
        {
            var revision = ++_navigationRevision;
            var section = model.Navigation.Selected;
            _restoringScrollRevision = revision;
            return Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    if (!_pageActive || !model.IsAuthenticated || revision != _navigationRevision
                        || model.Navigation.Selected != section)
                        return;
                    if (section == FDriverWorkspaceSection.Delivery && !string.IsNullOrWhiteSpace(focus))
                        await ScrollToEntryFocusAsync(focus);
                    else
                        await ScrollFor(section).ScrollToAsync(0, model.Navigation.GetScrollY(section), false);
                }
                finally
                {
                    if (_restoringScrollRevision == revision)
                        _restoringScrollRevision = null;
                }
            });
        }

        private async Task ScrollToEntryFocusAsync(string? focus)
        {
            if (BindingContext is not MainPageModel { IsAuthenticated: true } model)
            {
                return;
            }

            if (!model.Navigation.IsDelivery || string.IsNullOrWhiteSpace(focus))
            {
                await RestoreSectionScrollAsync(model, null);
                return;
            }

            var normalizedFocus = focus.Trim().ToLowerInvariant();
            var navigationRevision = _navigationRevision;
            var appearanceRevision = _appearanceRevision;
            if (normalizedFocus is "dispatch" or "bundle" or "delivery")
            {
                if (normalizedFocus == "delivery") model.IsCurrentDeliveryExpanded = model.HasActiveWork;
                else model.IsDeliveryDetailsExpanded = true;
                QueueFoodLayoutUpdate();
                await Dispatcher.DispatchAsync(() => { });
            }

            if (!_pageActive || !model.IsAuthenticated || !model.Navigation.IsDelivery
                || navigationRevision != _navigationRevision || appearanceRevision != _appearanceRevision)
                return;

            VisualElement? target = normalizedFocus switch
            {
                "dispatch" or "bundle" => RecommendationSection.IsVisible ? RecommendationSection : WorkspaceSummarySection,
                "delivery" => ActiveDeliverySection.IsVisible ? ActiveDeliverySection : WorkspaceSummarySection,
                "workspace" => WorkspaceSummarySection,
                _ => null
            };

            if (target is not null)
                await WorkspaceScroll.ScrollToAsync(target, ScrollToPosition.Start, false);
        }
    }
}
