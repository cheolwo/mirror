using Microsoft.Extensions.Logging;
using Hongdal.Ui.Common.Areas.App.Services;
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
        builder.Services.AddHongdalUiCommonAppServices();
        builder.Services.AddWarehouseManagerApplication();
        builder.Services.AddHongdalApiHttpClient(HongdalApiEndpoint.ResolveBaseAddress(
            builder.Configuration[HongdalApiEndpoint.ConfigurationKey],
            new Uri(HongdalApiEndpoint.LocalDevelopmentBaseAddress)));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
