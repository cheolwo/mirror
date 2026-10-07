using FDriverApp.PageModels;
using System.Globalization;

namespace FDriverApp.Pages;

public partial class CompletedDeliveryDetailPage : ContentPage, IQueryAttributable
{
    private readonly FDriverCompletedDeliveryDetailPageModel _model;
    private readonly IFDriverCompletedDeliveryNavigator _navigator;
    private readonly IFDriverProtectionSupportNavigator _supportNavigator;
    private DateOnly _listDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
    private bool _returning;
    public CompletedDeliveryDetailPage(FDriverCompletedDeliveryDetailPageModel model, IFDriverCompletedDeliveryNavigator navigator,
        IFDriverProtectionSupportNavigator supportNavigator)
    {
        InitializeComponent();
        BindingContext = _model = model;
        _navigator = navigator;
        _supportNavigator = supportNavigator;
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = query.TryGetValue(FDriverCompletedDeliveryNavigator.SettlementQueryKey, out var value)
            ? Uri.UnescapeDataString(Convert.ToString(value) ?? string.Empty) : string.Empty;
        if (query.TryGetValue(FDriverCompletedDeliveryNavigator.DateQueryKey, out var dateValue)
            && DateOnly.TryParseExact(Convert.ToString(dateValue), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) _listDate = date;
        var selection = query.TryGetValue(FDriverCompletedDeliveryNavigator.SelectionQueryKey, out var target)
            ? target as FDriverCompletedDeliveryNavigationTarget : null;
        if (selection is not null && selection.SettlementId != id) { _model.SetTarget(string.Empty); return; }
        _model.SetTarget(id, selection?.ExpectedOrderNo, selection?.ExpectedAttemptId);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _model.ActivateAsync();
    }
    protected override void OnDisappearing()
    {
        _model.Deactivate();
        base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() { _ = ReturnToListAsync(); return true; }
    private async void OnBackClicked(object? sender, EventArgs args) => await ReturnToListAsync();
    private async void OnLoginClicked(object? sender, EventArgs args) => await _navigator.ReturnToWorkspaceAsync();
    private async void OnProblemClicked(object? sender, EventArgs args)
    {
        if (!_model.IsLoading && _model.Detail is { } detail && !string.IsNullOrWhiteSpace(detail.Settlement.Display.OrderNo))
            await _supportNavigator.OpenAsync(detail.Settlement.Display.OrderNo);
    }
    private async Task ReturnToListAsync()
    {
        if (_returning) return;
        _returning = true;
        try
        {
            var stack = Shell.Current?.Navigation.NavigationStack;
            if (stack is { Count: > 1 } && stack[^2] is CompletedDeliveryListPage) await _navigator.BackAsync();
            else { await _navigator.ReturnToWorkspaceAsync(); await _navigator.OpenListAsync(_listDate); }
        }
        finally { _returning = false; }
    }
}
