namespace FDriverApp.Services;

public interface IFDriverWorkspaceNavigator
{
    Task OpenAsync(string? focus = null);
}

public sealed class FDriverWorkspaceNavigator : IFDriverWorkspaceNavigator
{
    public const string WorkspaceRoute = "food-delivery-workspace";
    public const string FocusQueryKey = "focus";

    public Task OpenAsync(string? focus = null)
    {
        var target = string.IsNullOrWhiteSpace(focus)
                ? "//" + WorkspaceRoute
                : $"//{WorkspaceRoute}?{FocusQueryKey}={Uri.EscapeDataString(focus.Trim())}";

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(target);
        });
    }
}
