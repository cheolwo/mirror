using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using SellerApp.Services;
using SellerApp.ViewModels;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SellerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        builder.Services.AddSingleton<IClientSecureTokenStore, MauiSecureTokenStore>();
        builder.Services.AddSingleton<SellerAuthSession>();
        builder.Services.AddSingleton<SellerMarketProfileService>();
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey]));
        builder.Services.AddSsalddelUiCommonAppServices<SellerAuthSession>();
        builder.Services.AddScoped<SellerAuthService>();
        builder.Services.AddScoped<SellerExportLedgerService>();
        builder.Services.AddScoped<SellerForeignFoodFacilityService>();
        builder.Services.AddTransient<SellerInventoryPageViewModel>();
        builder.Services.AddTransient<SellerProductsPageViewModel>();
        builder.Services.AddTransient<SellerProductCreatePageViewModel>();
        builder.Services.AddTransient<SellerListingsPageViewModel>();
        builder.Services.AddTransient<SellerListingCreatePageViewModel>();
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
