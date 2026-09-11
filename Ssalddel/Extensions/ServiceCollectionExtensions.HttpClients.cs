using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.External.Customs;
using 살뜰.Services.External.Google;
using 살뜰.Services.External.Gemini;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.HIOPSAI;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using Ssalddel.Services.Food;
using Ssalddel.Services.Education;
using Ssalddel.Services.Community;
using Ssalddel.Services.External.Typecast;
using Ssalddel.Services.External.YouTube;
using Ssalddel.Services.External.HongikHakdang;
using Ssalddel.Services.External.Naver;
using 살뜰.Services.Weather;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<I픽업지기상관측Client, 기상청초단기실황Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.KmaUltraShortNowcast.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddScoped<ICommunityNearbyRestaurantDirectory, MainServerCommunityNearbyRestaurantDirectory>();
        services.AddHttpClient<I교육기관제출전송Service, 교육기관제출전송Service>();
        services.AddHttpClient<ICommunityTextTranslationProvider, AzureCommunityTextTranslationProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommunityPostTranslationOptions>>().Value;
            client.BaseAddress = new Uri($"{options.Endpoint.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<ITypecastClient, TypecastClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TypecastOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IYouTubeDataApiClient, YouTubeDataApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<YouTubeOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IHongikHakdangCardSourceClient, HongikHakdangCardSourceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HongikHakdangCardOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));
        });
        services.AddHttpClient<IGeocodingService, GoogleGeocodingService>();
        services.AddHttpClient<IRouteDistanceService, GoogleRouteDistanceService>();
        services.AddHttpClient<INaverCloudDirectionsService, NaverCloudDirectionsService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NaverCloudDirectionsOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHttpClient<IOpinetAveragePriceService, OpinetAveragePriceService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpinetOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHttpClient<INtsBusinessRegistrationService, NtsBusinessRegistrationService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NtsBusinessRegistrationOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHttpClient<I해외제조업소조회Service, 해외제조업소조회Service>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<해외제조업소조회Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I수입식품제품조회Service, 수입식품제품조회Service>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<수입식품제품조회Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I수입식품한글표시사항조회Service, 수입식품한글표시사항조회Service>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<수입식품한글표시사항조회Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<ITossPaymentsService, TossPaymentsService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TossPaymentsOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHttpClient<IImageGenerationProviderClient, NanoBananaImageGenerationClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiImageOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 10, 600));
        });
        services.AddHttpClient<IAppContextImageBatchProviderClient, NanoBananaImageBatchClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiImageBatchOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 10, 900));
        });
        services.AddHttpClient<INaverMapsReverseGeocodingService, NaverMapsReverseGeocodingService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NaverMapsOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<IHIOPSAIClient, HIOPSAIClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HIOPSAIOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I개인통관부호검증Service, Unipass개인통관부호검증Service>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CustomsOptions>>().Value;
            client.BaseAddress = new Uri(options.UnipassBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I화물통관진행조회Service, Unipass화물통관진행조회Service>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CustomsOptions>>().Value;
            client.BaseAddress = new Uri(options.CargoTrackingBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IRoadAddressLookupService, RoadAddressLookupService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.RoadAddress.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IApartmentComplexLookupService, ApartmentComplexLookupService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.ApartmentComplex.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IApartmentManagementFeeLookupService, ApartmentManagementFeeLookupService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.ApartmentManagementFee.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I수협유통공공데이터Client, 수협유통공공데이터Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.DataGoKrBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I공동주택운영공공데이터Client, 공동주택운영공공데이터Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.DataGoKrBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I국문관광정보공공데이터Client, 국문관광정보공공데이터Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.DataGoKrBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I온라인가격공공데이터Client, 온라인가격공공데이터Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.DataGoKrBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IKosis비교자료공공데이터Client, Kosis비교자료공공데이터Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.DataGoKrBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I수산물산지위판가격Client, Mafra수산물산지위판가격Client>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.FisheriesAuction.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I경기데이터드림ApiClient, 경기데이터드림ApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.GyeonggiDataDream.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<I경기데이터드림CatalogClient, 경기데이터드림CatalogClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.GyeonggiDataDream.PortalBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddScoped<I경기데이터드림가축사육집계Client, 경기데이터드림가축사육집계Client>();
        services.AddHttpClient<IHsCountryTradeUnitPriceLookupService, HsCountryTradeUnitPriceLookupService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.CustomsTradeStatistics.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IFishCooperativeStatisticsClient, FishCooperativeStatisticsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.FishCooperativeStatistics.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<세관장확인대상물품공공데이터수집기>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.CustomsRequirements.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<관세환율공공데이터수집기>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicDataOptions>>().Value;
            client.BaseAddress = new Uri(options.CustomsExchangeRate.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddScoped<IDriverRecommendationPushService, FcmDriverRecommendationPushService>();
        services.AddHttpClient<IFcmPushService, FirebaseFcmPushService>();
        services.AddHttpClient<IKakaoAlimTalkService, KakaoAlimTalkService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KakaoAlimTalkOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });
        services.AddHttpClient<IKakao좌표변환Service, Kakao좌표변환Service>();

        return services;
    }
}
