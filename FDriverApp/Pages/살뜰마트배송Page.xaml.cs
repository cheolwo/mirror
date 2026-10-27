using FDriverApp.PageModels;
using FDriverApp.Services;

namespace FDriverApp.Pages;

public partial class 살뜰마트배송Page : ContentPage
{
    public const string RouteName = "mart-delivery-workspace";
    private readonly 살뜰마트배송PageViewModel _model;
    public 살뜰마트배송Page(살뜰마트배송PageViewModel model)
    { InitializeComponent(); BindingContext = _model = model; }
    protected override async void OnAppearing() { base.OnAppearing(); await _model.ActivateAsync(); }
    protected override void OnDisappearing() { _model.Deactivate(); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed()
    {
        if (!_model.Completion.HasNotice) return base.OnBackButtonPressed();
        _model.Completion.Dismiss(); return true;
    }
    private async void OnBackClicked(object? sender, EventArgs args)
    {
        if (Shell.Current is { } shell)
            await shell.GoToAsync(shell.Navigation.NavigationStack.Count > 1 ? ".."
                : "//" + FDriverWorkspaceNavigator.WorkspaceRoute + "?focus=profile");
    }
}
