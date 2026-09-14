using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using MudBlazor.Services;
using OrdererApp.Services;
using OrdererApp.ViewModels;

namespace OrdererApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            optional: true,
            reloadOnChange: false);

        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddOrdererSecurityServices();
        builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, OrdererPlatformCommunityNodeNavigationResolver>();
        builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, OrdererPlatformHomeWorkspaceNavigationResolver>();
        builder.Services.AddSsalddelUiCommonAppServices<OrdererAccessTokenProvider>();
        builder.Services.AddTransient<주문자Controller기능모음ViewModel>();
        builder.Services.AddTransient<음식Controller기능모음ViewModel>();
        builder.Services.AddTransient<주문자공동구매기능ViewModel>();
        builder.Services.AddScoped<IGroupPurchaseProductCatalogService, HttpGroupPurchaseProductCatalogService>();
        builder.Services.AddScoped<I같이주문여정ReadService, Http같이주문여정ReadService>();
        builder.Services.AddTransient<GroupPurchaseCatalogViewModel>();
        builder.Services.AddScoped<GroupPurchaseWishBatchViewModel>();
        builder.Services.AddTransient<주문자재료후보PageViewModel>();
        builder.Services.AddTransient<주문자의향등록PageViewModel>();
        builder.Services.AddTransient<주문자Api기능모음ViewModel>();
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
                new Uri(SsalddelServerEndpoint.LocalDevelopmentBaseAddress)),
            ServiceLifetime.Singleton);
        builder.Services.AddScoped<IGroupPurchaseShipmentTrackingService, HttpGroupPurchaseShipmentTrackingService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
