using FDriverApp.PageModels;
using FDriverApp.Services;

namespace FDriverApp.Pages;

public partial class ReturnAreaSettingsPage : ContentPage
{
    public const string RouteName = "food-return-area-settings";
    private readonly MainPageModel _model;
    private bool _returning;

    public ReturnAreaSettingsPage(MainPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 같은 기사 계정의 확인된 설정과 미저장 초안은 다시 조회해서 덮어쓰지 않습니다.
        if (_model.IsAuthenticated && !_model.ReturnAreaSettingsLoaded)
            await _model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
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
