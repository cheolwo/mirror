using FDriverApp.PageModels;
using System.ComponentModel;
using System.Globalization;

namespace FDriverApp.Pages;

public partial class CompletedDeliveryListPage : ContentPage, IQueryAttributable
{
    private readonly FDriverCompletedDeliveryListPageModel _model;
    private readonly IFDriverCompletedDeliveryNavigator _navigator;
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
        _model.PropertyChanged -= ModelChanged;
        _model.PropertyChanged += ModelChanged;
        await _model.ActivateAsync();
    }
    protected override void OnDisappearing()
    {
        _model.PropertyChanged -= ModelChanged;
        _model.Deactivate();
        base.OnDisappearing();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FDriverCompletedDeliveryListPageModel.PageNumber) && _model.Items.Count > 0)
            Dispatcher.Dispatch(() => { if (_model.Items.Count > 0) DeliveryList.ScrollTo(0, position: ScrollToPosition.Start, animate: false); });
    }
    private async void OnBackClicked(object? sender, EventArgs args) => await _navigator.BackAsync();
    private async void OnLoginClicked(object? sender, EventArgs args) => await _navigator.ReturnToWorkspaceAsync();
}
