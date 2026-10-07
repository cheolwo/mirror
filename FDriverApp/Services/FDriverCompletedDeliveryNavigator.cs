using FDriverApp.Pages;
using System.Globalization;

namespace FDriverApp.Services;

public sealed class FDriverCompletedDeliveryNavigator : IFDriverCompletedDeliveryNavigator
{
    public const string ListRoute = "food-delivery-history";
    public const string DetailRoute = "food-delivery-history-detail";
    public const string DateQueryKey = "date";
    public const string SettlementQueryKey = "settlementId";
    public const string SelectionQueryKey = "selection";
    private bool _navigating;

    public Task OpenListAsync(DateOnly? date = null) => NavigateAsync(async shell =>
    {
        var route = ListRoute + (date.HasValue
            ? "?date=" + date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty);
        await shell.GoToAsync(route);
    });

    public Task OpenDetailAsync(FDriverCompletedDeliveryNavigationTarget target) => NavigateAsync(async shell =>
    {
        if (string.IsNullOrWhiteSpace(target.SettlementId)) return;
        var route = DetailRoute + "?settlementId=" + Uri.EscapeDataString(target.SettlementId)
            + "&date=" + target.CompletionDateKst.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await shell.GoToAsync(route, new ShellNavigationQueryParameters { [SelectionQueryKey] = target });
    });

    public Task BackAsync() => NavigateAsync(async shell =>
    {
        if (shell.Navigation.NavigationStack.Count > 1) await shell.GoToAsync("..");
        else await shell.GoToAsync("//" + FDriverWorkspaceNavigator.WorkspaceRoute);
    });

    public Task ReturnToWorkspaceAsync() => NavigateAsync(
        shell => shell.GoToAsync("//" + FDriverWorkspaceNavigator.WorkspaceRoute));

    private Task NavigateAsync(Func<Shell, Task> navigate)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_navigating || Shell.Current is not { } shell) return;
            _navigating = true;
            try { await navigate(shell); }
            finally { _navigating = false; }
        });
}
