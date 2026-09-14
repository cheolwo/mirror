using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using MudBlazor.Services;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using RestaurantDeskApp.Services.Security;
using RestaurantDeskApp.ViewModels;
using Ssalddel.Client.Infrastructure.Security;

namespace RestaurantDeskApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: true,
            reloadOnChange: false);
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            optional: true,
            reloadOnChange: false);
        var restaurantLegacyBaseAddress =
            builder.Configuration[RestaurantDeskOptions.SectionName + ":ServerBaseUrl"];
        var operationalApiBaseAddress =
            SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
                SsalddelServerEndpoint.ResolveBaseAddress(
                    restaurantLegacyBaseAddress,
                    SsalddelServerEndpoint.CreateDefaultBaseAddress()));
        builder.Services.Configure<RestaurantDeskOptions>(builder.Configuration.GetSection(RestaurantDeskOptions.SectionName));
        builder.Services.PostConfigure<RestaurantDeskOptions>(options =>
            options.ServerBaseUrl = operationalApiBaseAddress.AbsoluteUri);
        builder.Services.Configure<RestaurantOrderAlertOptions>(builder.Configuration.GetSection(RestaurantOrderAlertOptions.SectionName));
        builder.Services.AddSingleton<IClientSecureTokenStore, RestaurantMauiSecureTokenStore>();
        builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        builder.Services.AddSingleton<ClientAuthSession>();
        builder.Services.AddSingleton<RestaurantAccessTokenProvider>();
        builder.Services.AddHttpClient<RestaurantAuthService>((sp, client) =>
        {
            client.BaseAddress = operationalApiBaseAddress;
        });
        builder.Services.AddSingleton<RestaurantDeskSampleService>();
        builder.Services.AddSingleton<I음식점식재료공급요청Service, RestaurantIngredientSupplySampleService>();
        builder.Services.AddSingleton<I주문알림Service, 주문알림Service>();
        builder.Services.AddSingleton<I음식점주문SignalRClientService, 음식점주문SignalRClientService>();
        builder.Services.AddSingleton<I음식점조리시간설정Service, 음식점조리시간설정Service>();
        builder.Services.AddSsalddelUiCommonAppServices<RestaurantAccessTokenProvider>();
        builder.Services.AddTransient<음식Controller기능모음ViewModel>();
        builder.Services.AddTransient<음식점주문조회ViewModel>();
        builder.Services.AddTransient<음식점주문접수ViewModel>();
        builder.Services.AddTransient<음식점주문이행ViewModel>();
        builder.Services.AddTransient<음식점주문기능ViewModel>();
        builder.Services.AddTransient<음식점Api기능모음ViewModel>();
        builder.Services.AddTransient<음식점식재료공급요청작성ViewModel>();
        builder.Services.AddTransient<음식점식재료공급비교ViewModel>();
        builder.Services.AddTransient<음식점식재료공급진행조회ViewModel>();
        builder.Services.AddTransient<음식점식재료공급요청PageViewModel>();
        builder.Services.AddSsalddelDocumentOutputServices();
        builder.Services.AddHttpClient<I음식주문ApiClient, Ssalddel음식주문Client>((sp, client) =>
        {
            client.BaseAddress = operationalApiBaseAddress;
        });
        builder.Services.AddSingleton<음식점전표DraftFactory>();
        builder.Services.AddSingleton<I음식점주문DeskService, 음식점주문DeskService>();
        builder.Services.AddScoped<배차주소ApiService>();
        builder.Services.AddSsalddelOperationalApiHttpClient(
            operationalApiBaseAddress);
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
