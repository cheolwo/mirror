using Hongdal.Client.Infrastructure.Notifications;
using Hongdal.Client.Infrastructure.Security;
using HongdalApp.Services.Application;
using HongdalApp.Services.CommonContents;
using HongdalApp.Services.Localization;
using HongdalApp.Services.Samples;
using HongdalApp.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

internal static class ShipperPlatformModule
{
    internal static IServiceCollection AddShipperPlatformModule(this IServiceCollection services)
    {
        services.AddSingleton<IClientSecureTokenStore, MauiSecureTokenStore>();
        services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        services.AddSingleton<IAuthSession, AuthSession>();
        services.AddSingleton<IHongdalMobilePushTokenProvider, NullHongdalMobilePushTokenProvider>();
        services.AddScoped<AuthApiService>();
        services.AddSingleton<I꾸미기보유권LocalStore, Maui꾸미기보유권LocalStore>();
        services.AddScoped<꾸미기보유권동기화Service>();
        services.AddSingleton<HongdalClientRoleService>();
        services.AddSingleton<ShipperOperatingProfileService>();
        services.AddSingleton<IWarehouseWorkEntryGateService, SampleWarehouseWorkEntryGateService>();
        services.AddSingleton<ShipperViewVisibilityService>();
        services.AddSingleton<ShipperLocalizationService>();
        services.AddSingleton<탐색문의샘플Service>();
        services.AddSingleton<IShipperExplorationInquiryService>(provider =>
            provider.GetRequiredService<탐색문의샘플Service>());
        services.AddSingleton<I화주공통콘텐츠Service, 샘플화주공통콘텐츠Service>();
        services.AddSingleton<IAppEventPublisher, AppEventPublisher>();
        return services;
    }
}
