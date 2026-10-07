using DriverApp.Services.CommonContents;
using DriverApp.Services.Samples;
using DriverApp.Services.Security;
using DriverApp.ViewModels.Driver.Features;
using DriverApp.ViewModels.Driver.Home;
using DriverApp.ViewModels.Driver.Notification;
using DriverApp.ViewModels.Driver.Recommendation;
using DriverApp.ViewModels.Driver.Reservation;
using DriverApp.ViewModels.Driver.Settlement;
using DriverApp.ViewModels.Driver.Transport;
using DriverApp.ViewModels.Driver.Work;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriverApp.Services;

public static class DriverServiceCollectionExtensions
{
    public static IServiceCollection AddDriverAppServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<ClientDataModeOptions>(configuration.GetSection(ClientDataModeOptions.SectionName));
        }
        else
        {
            services.Configure<ClientDataModeOptions>(_ => { });
        }

        services.AddSingleton<DriverAppProfile>();
        services.AddSingleton<DriverOperatingProfileService>();
        services.AddSingleton<IClientSecureTokenStore, MauiSecureTokenStore>();
        services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        services.AddSingleton<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
        services.AddSingleton<IAuthSession, AuthSession>();
        services.AddSingleton<AuthApiService>();
        services.AddSingleton(provider =>
        {
            var httpClient = provider.GetRequiredService<HttpClient>();
            var authSession = provider.GetRequiredService<IAuthSession>();
            var authApiService = provider.GetRequiredService<AuthApiService>();
            return new TransportRequestLedgerRealtimeClient(
                httpClient.BaseAddress
                ?? throw new InvalidOperationException("Ssalddel API BaseAddress가 설정되어 있지 않습니다."),
                async cancellationToken =>
                {
                    var error = await authApiService.EnsureAccessTokenAsync(
                        cancellationToken: cancellationToken);
                    return string.IsNullOrWhiteSpace(error) ? authSession.AccessToken : null;
                },
                provider.GetRequiredService<ITransportRequestLedgerObserver>());
        });
        services.AddSingleton<DriverAccessPolicyService>();
        services.AddSingleton<DriverHomeDisplayPreferenceService>();
        services.AddSingleton<IDriverHomeMapService, DriverHomeMapService>();
        services.AddSingleton<DriverHomeRoutePlanningService>();
        services.AddSingleton<IDriverApiClient, DriverApiClient>();
        services.AddSingleton<IDriverProfileApiService, DriverProfileApiService>();
        services.AddSingleton<IDriverWorkApiService, DriverWorkApiService>();
        services.AddSingleton<IDriverRecommendationApiService, DriverRecommendationApiService>();
        services.AddSingleton<IDriverExplorationCampaignApiService, DriverExplorationCampaignApiService>();
        services.AddSingleton<IDriverDispatchActionApiService, DriverDispatchActionApiService>();
        services.AddSingleton<IDriverFoodDeliveryApiService, DriverFoodDeliveryApiService>();
        services.AddSingleton<IDriverReservationApiService, DriverReservationApiService>();
        services.AddSingleton<IDriverSettingsApiService, DriverSettingsApiService>();
        services.AddSingleton<IDriverSettlementApiService, DriverSettlementApiService>();
        services.AddSingleton<IDriverNotificationApiService, DriverNotificationApiService>();
        services.AddSingleton<IDriverCommandFeatureSettingsApiService, DriverCommandFeatureSettingsApiService>();
        services.AddSingleton<IDriverDevelopmentApiService, DriverDevelopmentApiService>();
        services.AddSingleton<I기사푸시토큰등록Service, 기사푸시토큰등록Service>();
#if ANDROID
        services.AddSingleton<I기사위치송신Service, Android기사위치송신Service>();
#else
        services.AddSingleton<I기사위치송신Service, Noop기사위치송신Service>();
#endif
        services.AddSingleton<추천카드표시설정Service>();
        services.AddSingleton<DriverRecommendationDecisionService>();
        services.AddSingleton<IDriverRecommendationDecisionService>(sp => sp.GetRequiredService<DriverRecommendationDecisionService>());
        services.AddSingleton<IDriverRecommendationNotificationService, SampleDriverRecommendationNotificationService>();
        services.AddSingleton<기사샘플데이터Service>();
        services.AddSingleton<ServerBackedDriverSampleDataService>();
        services.AddSingleton<IDriverSampleDataService>(sp => sp.GetRequiredService<ServerBackedDriverSampleDataService>());
        services.AddSingleton<I공통콘텐츠Service, Http공통콘텐츠Service>();
        services.AddSingleton<DriverViewVisibilityService>();
        services.AddSingleton<IDriverNativeMapNavigator, DriverNativeMapNavigator>();
        services.AddSingleton<IDriverTransportApiService, DriverTransportApiService>();
        services.AddSingleton<IDriverFreightWorkspaceStore, DriverFreightWorkspaceStore>();
        services.AddSingleton<HttpDriverTransportCompletionPhotoService>();
        services.AddSingleton<IDriverTransportCompletionPhotoService>(sp => sp.GetRequiredService<HttpDriverTransportCompletionPhotoService>());
        services.AddSingleton<HttpDriverTransportExceptionService>();
        services.AddSingleton<IDriverTransportExceptionService>(sp => sp.GetRequiredService<HttpDriverTransportExceptionService>());
        services.AddScoped<IApiClient, ApiClient>();
        services.AddTransient<기사프로필기능ViewModel>();
        services.AddTransient<기사근무기능ViewModel>();
        services.AddTransient<기사추천기능ViewModel>();
        services.AddTransient<기사탐색캠페인기능ViewModel>();
        services.AddTransient<기사배차액션기능ViewModel>();
        services.AddTransient<기사예약기능ViewModel>();
        services.AddTransient<기사운송조회ViewModel>();
        services.AddTransient<기사상차업무ViewModel>();
        services.AddTransient<기사하차업무ViewModel>();
        services.AddTransient<기사운송예외업무ViewModel>();
        services.AddTransient<기사운송기능ViewModel>();
        services.AddTransient<기사설정기능ViewModel>();
        services.AddTransient<기사정산기능ViewModel>();
        services.AddTransient<기사알림기능ViewModel>();
        services.AddTransient<기사Command기능설정ViewModel>();
        services.AddTransient<기사개발도구기능ViewModel>();
        services.AddTransient<기사Controller기능모음ViewModel>();
        services.AddTransient<기사Api기능모음ViewModel>();
        services.AddTransient<기사홈PageViewModel>();
        services.AddTransient<기사운행시작PageViewModel>();
        services.AddTransient<기사탐색캠페인PageViewModel>();
        services.AddTransient<기사예약PageViewModel>();
        services.AddTransient<기사월정산PageViewModel>();
        services.AddTransient<기사계좌정보PageViewModel>();
        services.AddTransient<기사이용료안내PageViewModel>();
        services.AddTransient<기사알림함PageViewModel>();
        services.AddTransient<기사푸시설정PageViewModel>();
        services.AddTransient<기사상차완료ViewModel>();
        services.AddTransient<기사하차완료ViewModel>();
        services.AddTransient<기사운송상세조회Service>();
        services.AddTransient<기사상차PageViewModel>();
        services.AddTransient<기사하차PageViewModel>();
        services.AddTransient<기사운송내역PageViewModel>();

        return services;
    }
}
