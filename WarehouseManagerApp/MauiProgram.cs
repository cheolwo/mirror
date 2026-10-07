using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.Services;
using MudBlazor.Services;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddWarehouseManagerApplication();
        builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, WarehousePlatformCommunityNodeNavigationResolver>();
        builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, WarehousePlatformHomeWorkspaceNavigationResolver>();
        builder.Services.AddSsalddelUiCommonAppServices<WarehouseAccessTokenProvider>();
#if DEBUG || SSALDDEL_USB_FIELD_TEST
        const bool allowInsecureDebugEndpoint = true;
#else
        const bool allowInsecureDebugEndpoint = false;
#endif
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveMobileBaseAddress(
                typeof(MauiProgram).Assembly,
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
                allowInsecureDebugEndpoint));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
