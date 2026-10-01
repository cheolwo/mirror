using Ssalddel.Services.Content;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.Customs;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddSsalddelFoundationDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<ISsalddelExecutionModePolicy, SsalddelExecutionModePolicy>();
        services.AddRoleAdvertisingIntegration();
        services.AddScoped<I결제Provider, Toss결제Provider>();
        services.AddScoped<I공통결제Service, 공통결제Service>();
        services.AddScoped<I콘텐츠혜택계산Service, 콘텐츠혜택계산Service>();
        services.AddScoped<I결제승인완료OutboxService, 결제승인완료OutboxService>();
        services.AddOptions<음식주문결제승인Options>().BindConfiguration(음식주문결제승인Options.SectionName);
        services.AddScoped<음식주문결제승인OutboxService>();
        services.AddScoped<통관상태동기화Service>();
        services.AddSingleton<IPublicDataApiMetadataCatalog, PublicDataApiMetadataCatalog>();
        services.AddSingleton<IExternalDataSourceCatalog, ExternalDataSourceCatalog>();
        services.AddSingleton<IExternalDataCredentialProvider, ConfigurationExternalDataCredentialProvider>();
        services.AddSingleton<IExternalDataCollectionPolicy, ConfigurationExternalDataCollectionPolicy>();
        services.AddSingleton<IExternalDataRetryDelay, SystemExternalDataRetryDelay>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IExternalDataRawStorage, ExternalDataRawObjectStorage>();
        services.AddScoped<EfExternalDataIngestionStore>();
        services.AddScoped<I외부데이터수집Store>(provider =>
            provider.GetRequiredService<EfExternalDataIngestionStore>());
        services.AddScoped<I외부지역MappingStore>(provider =>
            provider.GetRequiredService<EfExternalDataIngestionStore>());
        services.AddScoped<IExternalDataIngestionRuntime, ExternalDataIngestionRuntime>();
        services.AddAgriculturalExternalDataProviders();
        services.AddKoreaAdministrativeRegionPublicDataProviders();
        services.AddSingleton<I공공데이터포털활용ApiModuleCatalog, 공공데이터포털활용ApiModuleCatalog>();
        services.AddScoped<IHs공공데이터수집Service, Hs공공데이터수집Service>();
        services.AddScoped<IHs공공데이터수집기, Hs수입평균단가공공데이터수집기>();
        services.AddScoped<IHs공공데이터수집기>(sp =>
            sp.GetRequiredService<세관장확인대상물품공공데이터수집기>());
        services.AddScoped<IHs공공데이터수집기>(sp =>
            sp.GetRequiredService<관세환율공공데이터수집기>());

        return services;
    }
}
