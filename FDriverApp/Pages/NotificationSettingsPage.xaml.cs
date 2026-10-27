using FDriverApp.PageModels;
using FDriverApp.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FDriverApp.Pages;

public partial class NotificationSettingsPage : ContentPage
{
    public const string RouteName = "food-notification-settings";
    private readonly FDriverNotificationSettingsPageModel _model;
    public NotificationSettingsPage(FDriverNotificationSettingsPageModel model)
    {
        InitializeComponent(); BindingContext = _model = model;
        // Compiled string bindings do not turn path markup into the Geometry required by Path.Data.
        var geometry = new PathGeometryConverter();
        FoodNotificationIcon.Data = (Geometry)geometry.ConvertFromInvariantString(model.FoodIconPath)!;
        CargoNotificationIcon.Data = (Geometry)geometry.ConvertFromInvariantString(model.CargoIconPath)!;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await _model.ActivateAsync(); }
    protected override void OnDisappearing() { _model.Deactivate(); base.OnDisappearing(); }
    private async void OnBackClicked(object? sender, EventArgs args)
    {
        if (Shell.Current is { } shell) await shell.GoToAsync(shell.Navigation.NavigationStack.Count > 1 ? ".." : "//" + FDriverWorkspaceNavigator.WorkspaceRoute + "?focus=profile");
    }
    private void OnDeviceSettingsClicked(object? sender, EventArgs args)
    {
#if ANDROID
        var context = global::Android.App.Application.Context;
        var intent = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
            ? new global::Android.Content.Intent(global::Android.Provider.Settings.ActionAppNotificationSettings)
                .PutExtra(global::Android.Provider.Settings.ExtraAppPackage, context.PackageName)
            : new global::Android.Content.Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                global::Android.Net.Uri.Parse("package:" + context.PackageName));
        intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
#endif
    }
}
