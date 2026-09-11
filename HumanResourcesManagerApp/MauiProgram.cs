using Ssalddel.Ui.Common.Areas.App.Services;
using HumanResourcesManagerApp.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace HumanResourcesManagerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddHumanResourcesManagerApplication();
        builder.Services.AddSsalddelUiCommonAppServices<HumanResourcesAccessTokenProvider>();
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
