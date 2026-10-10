using FDriverApp.PageModels;
using System.ComponentModel;
using System.Globalization;

namespace FDriverApp.Pages;

public partial class CompletedDeliveryListPage : ContentPage, IQueryAttributable
{
    private readonly FDriverCompletedDeliveryListPageModel _model;
    private readonly IFDriverCompletedDeliveryNavigator _navigator;
    private bool _pageActive;
    private bool _restoringScrollPosition;
    private long _appearanceRevision;
    public CompletedDeliveryListPage(FDriverCompletedDeliveryListPageModel model, IFDriverCompletedDeliveryNavigator navigator)
    {
        InitializeComponent();
        BindingContext = _model = model;
        _navigator = navigator;
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(FDriverCompletedDeliveryNavigator.DateQueryKey, out var value)
            && DateOnly.TryParseExact(Convert.ToString(value), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            _model.SetDate(date);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pageActive = true;
        _restoringScrollPosition = true;
        var revision = ++_appearanceRevision;
        _model.PropertyChanged -= ModelChanged;
        _model.PropertyChanged += ModelChanged;
        await _model.ActivateAsync();
        Dispatcher.Dispatch(() =>
        {
            if (!_pageActive || revision != _appearanceRevision) return;
            if (_model.ScrollRestoreIndex is { } index)
                DeliveryList.ScrollTo(index, position: ScrollToPosition.Start, animate: false);
            _restoringScrollPosition = false;
        });
    }
    protected override void OnDisappearing()
    {
        _pageActive = false;
        _restoringScrollPosition = false;
        ++_appearanceRevision;
        _model.PropertyChanged -= ModelChanged;
        _model.Deactivate();
        base.OnDisappearing();
    }
    private void OnDeliveryListScrolled(object? sender, ItemsViewScrolledEventArgs args)
    {
        if (_pageActive && !_restoringScrollPosition)
            _model.RememberScrollPosition(args.FirstVisibleItemIndex);
    }
    private async void OnDeliverySelected(object? sender, SelectionChangedEventArgs args)
    {
        var item = args.CurrentSelection.FirstOrDefault() as FDriverDailySettlementItem;
        // 돌아온 뒤 같은 행도 다시 선택할 수 있도록 선택 표시는 바로 해제합니다.
        DeliveryList.SelectedItem = null;
        if (_pageActive && item?.OpenDetailsCommand is { } command)
            await command.ExecuteAsync(null);
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FDriverCompletedDeliveryListPageModel.PageNumber) && _model.Items.Count > 0)
        {
            var revision = _appearanceRevision;
            var date = _model.SelectedDate;
            var page = _model.PageNumber;
            var firstItem = _model.Items[0];
            Dispatcher.Dispatch(() =>
            {
                if (_pageActive && revision == _appearanceRevision
                    && date == _model.SelectedDate && page == _model.PageNumber
                    && _model.Items.Count > 0 && ReferenceEquals(firstItem, _model.Items[0]))
                    DeliveryList.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
            });
        }
    }
    private async void OnBackClicked(object? sender, EventArgs args) => await _navigator.BackAsync();
    private async void OnLoginClicked(object? sender, EventArgs args) => await _navigator.ReturnToWorkspaceAsync();
}
