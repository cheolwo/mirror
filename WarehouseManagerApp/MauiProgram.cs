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
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
                new Uri(SsalddelServerEndpoint.LocalDevelopmentBaseAddress)));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
