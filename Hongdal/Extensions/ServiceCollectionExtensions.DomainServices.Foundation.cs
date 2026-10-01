using Hongdal.Services.Content;
using 홍달.Services.External.Customs;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;
using 홍달.Services.Payments;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddHongdalFoundationDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<IHongdalExecutionModePolicy, HongdalExecutionModePolicy>();
        services.AddRoleAdvertisingIntegration();
        services.AddScoped<I결제Provider, Toss결제Provider>();
        services.AddScoped<I공통결제Service, 공통결제Service>();
        services.AddScoped<I콘텐츠혜택계산Service, 콘텐츠혜택계산Service>();
        services.AddScoped<I결제승인완료OutboxService, 결제승인완료OutboxService>();
        services.AddScoped<통관상태동기화Service>();
        services.AddSingleton<IPublicDataApiMetadataCatalog, PublicDataApiMetadataCatalog>();
        services.AddScoped<IHs공공데이터수집Service, Hs공공데이터수집Service>();
        services.AddScoped<IHs공공데이터수집기, Hs수입평균단가공공데이터수집기>();
        services.AddScoped<IHs공공데이터수집기>(sp =>
            sp.GetRequiredService<세관장확인대상물품공공데이터수집기>());
        services.AddScoped<IHs공공데이터수집기>(sp =>
            sp.GetRequiredService<관세환율공공데이터수집기>());

        return services;
    }
}
