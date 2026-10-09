using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Reflection;
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
#if DEBUG || SSALDDEL_USB_FIELD_TEST
        const bool allowInsecureDebugEndpoint = true;
#else
        const bool allowInsecureDebugEndpoint = false;
#endif
        var operationalApiBaseAddress =
            SsalddelServerEndpoint.ResolveMobileBaseAddress(
                typeof(MauiProgram).Assembly,
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey]
                ?? restaurantLegacyBaseAddress,
                allowInsecureDebugEndpoint);
        builder.Services.Configure<RestaurantDeskOptions>(builder.Configuration.GetSection(RestaurantDeskOptions.SectionName));
        builder.Services.PostConfigure<RestaurantDeskOptions>(options =>
            options.ServerBaseUrl = operationalApiBaseAddress.AbsoluteUri);
#if DEBUG
        var usbFoodTestFixture = typeof(MauiProgram).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "SsalddelUsbFoodTestFixture")?.Value;
        if (usbFoodTestFixture == "sagajeong-food-mobile-dispatch.r1"
            && operationalApiBaseAddress.Scheme == Uri.UriSchemeHttp
            && operationalApiBaseAddress.IsLoopback && operationalApiBaseAddress.Port == 5363
            && operationalApiBaseAddress.AbsolutePath == "/"
            && string.IsNullOrEmpty(operationalApiBaseAddress.UserInfo)
            && string.IsNullOrEmpty(operationalApiBaseAddress.Query)
            && string.IsNullOrEmpty(operationalApiBaseAddress.Fragment))
        {
            // 명시적인 USB 시험 판본에서만 서버의 합성 음식점 101과 결속한다.
            builder.Services.PostConfigure<RestaurantDeskOptions>(options =>
            {
                options.RestaurantId = 101;
                options.RestaurantName = "사가정 한상 샘플";
                options.RestaurantAddress = "서울특별시 중랑구 사가정역 내부 검증권역";
                options.RestaurantDetailAddress = string.Empty;
                options.RestaurantLatitude = 37.580450m;
                options.RestaurantLongitude = 127.087950m;
            });
        }
#endif
        builder.Services.Configure<RestaurantOrderAlertOptions>(builder.Configuration.GetSection(RestaurantOrderAlertOptions.SectionName));
        builder.Services.AddSingleton<IClientSecureTokenStore, RestaurantMauiSecureTokenStore>();
        builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        builder.Services.AddSingleton<ClientAuthSession>();
        builder.Services.AddSingleton<RestaurantAccessTokenProvider>();
        builder.Services.AddHttpClient("RestaurantAuthentication", (sp, client) =>
        {
            client.BaseAddress = operationalApiBaseAddress;
        });
        // Logout and definitive 401 must reach the same subscribers in all pages and API clients.
        builder.Services.AddSingleton(sp => new RestaurantAuthService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("RestaurantAuthentication"),
            sp.GetRequiredService<ClientAuthSession>()));
        builder.Services.AddSingleton<RestaurantMenuDraftStore>();
        builder.Services.AddSingleton<IRestaurantProgressPendingStore, RestaurantSecureProgressPendingStore>();
        builder.Services.AddSingleton<RestaurantDeskSampleService>();
        builder.Services.AddSingleton<I음식점식재료공급요청Service, RestaurantIngredientSupplySampleService>();
        builder.Services.AddSingleton<I주문알림Service, 주문알림Service>();
#if ANDROID
        builder.Services.AddSingleton<IRestaurantReceiptPrinter, Platforms.Android.AndroidRestaurantReceiptPrinter>();
        builder.Services.AddSingleton<IRestaurantOrderNotificationPlatform, Platforms.Android.AndroidRestaurantOrderNotifications>();
#else
        builder.Services.AddSingleton<IRestaurantReceiptPrinter, BrowserRestaurantReceiptPrinter>();
        builder.Services.AddSingleton<IRestaurantOrderNotificationPlatform, NoRestaurantOrderNotifications>();
#endif
        builder.Services.AddSingleton<RestaurantOrderNotificationCoordinator>();
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
        builder.Services.AddSingleton<RestaurantAgreedRateWorkspaceBridge>();
        builder.Services.AddSingleton<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.IRoleWorkspaceApi>(sp => sp.GetRequiredService<RestaurantAgreedRateWorkspaceBridge>());
        builder.Services.AddSingleton<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.IRoleWorkspaceAccess>(sp => sp.GetRequiredService<RestaurantAgreedRateWorkspaceBridge>());
        builder.Services.AddTransient<I음식점메뉴ApiClient>(sp =>
            (Ssalddel음식주문Client)sp.GetRequiredService<I음식주문ApiClient>());
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
