using FDriverApp.PageModels;
using FDriverApp.Services;

namespace FDriverApp.Pages;

public partial class DeliveryExperienceSettingsPage : ContentPage
{
    public const string RouteName = "food-delivery-experience-settings";
    private readonly MainPageModel _model;
    private bool _returning;

    public DeliveryExperienceSettingsPage(MainPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 저장 전 선택값과 확인된 판본은 기존 업무 VM이 계속 소유합니다.
        if (_model.IsAuthenticated && !_model.ExperienceSharingLoaded)
            await _model.LoadExperienceSharingCommand.ExecuteAsync(null);
    }

    protected override bool OnBackButtonPressed() { _ = ReturnAsync(); return true; }
    private async void OnBackClicked(object? sender, EventArgs args) => await ReturnAsync();
    private async void OnLoginClicked(object? sender, EventArgs args) => await ReturnAsync(workspace: true);

    private async Task ReturnAsync(bool workspace = false)
    {
        if (_returning || Shell.Current is not { } shell) return;
        _returning = true;
        try
        {
            await shell.GoToAsync(!workspace && shell.Navigation.NavigationStack.Count > 1
                ? ".." : "//" + FDriverWorkspaceNavigator.WorkspaceRoute + "?focus=profile");
        }
        finally { _returning = false; }
    }
}
