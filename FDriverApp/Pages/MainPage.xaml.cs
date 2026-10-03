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

        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
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
                await model.InitializeAsync();
                if (!_pageActive || appearanceRevision != _appearanceRevision)
                    return;
                await model.StartMonitoringAsync();
                if (!_pageActive || appearanceRevision != _appearanceRevision
                    || navigationRevision != _navigationRevision)
                    return;
                var focus = _entryFocus;
                if (model.IsAuthenticated && !model.HasWorkspaceWarning)
                    model.ApplyEntryFocus(focus);
                if (model.IsAuthenticated)
                    model.Navigation.TryApplyFocus(focus);
                _entryFocus = null;
                await ScrollToEntryFocusAsync(focus);
            }
        }

        protected override async void OnDisappearing()
        {
            RememberCurrentScroll();
            _pageActive = false;
            _appearanceRevision++;
            _navigationRevision++;
            _restoringScrollRevision = null;
            if (BindingContext is MainPageModel model)
            {
                model.PropertyChanged -= OnModelPropertyChanged;
                await model.StopMonitoringAsync();
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

        private async void OnSettlementClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Settlement);

        private async void OnDeliveryClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery);

        private async void OnRecommendationsClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery, "dispatch");

        private async void OnCurrentDeliveryClicked(object? sender, EventArgs args)
            => await ShowSectionAsync(FDriverWorkspaceSection.Delivery, "delivery");

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

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
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
            FDriverWorkspaceSection.Settlement => SettlementScroll,
            FDriverWorkspaceSection.Profile => ProfileScroll,
            _ => WorkspaceScroll
        };

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

            RememberCurrentScroll();
            _entryFocus = null;
            model.Navigation.Select(section);
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

        private Task ScrollToEntryFocusAsync(string? focus)
        {
            if (BindingContext is not MainPageModel { IsAuthenticated: true } model)
            {
                return Task.CompletedTask;
            }

            if (!model.Navigation.IsDelivery || string.IsNullOrWhiteSpace(focus))
                return RestoreSectionScrollAsync(model, null);

            VisualElement? target = focus?.Trim().ToLowerInvariant() switch
            {
                "dispatch" or "bundle" => RecommendationSection.IsVisible ? RecommendationSection : WorkspaceSummarySection,
                "delivery" => ActiveDeliverySection.IsVisible ? ActiveDeliverySection : WorkspaceSummarySection,
                "workspace" => WorkspaceSummarySection,
                _ => null
            };

            return target is null
                ? Task.CompletedTask
                : WorkspaceScroll.ScrollToAsync(target, ScrollToPosition.Start, false);
        }
    }
}
