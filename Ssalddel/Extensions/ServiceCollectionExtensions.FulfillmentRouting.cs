using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Fulfillment;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Food;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>기본 비활성화된 공통 자동 수행과 생활/음식 원천 adapter를 연결합니다.
    /// 실제 업체 네트워크 API나 청구·지급 provider는 등록하지 않습니다.</summary>
    public static IServiceCollection AddSsalddelFulfillmentRouting(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<FulfillmentRoutingOptions>().BindConfiguration(FulfillmentRoutingOptions.SectionName);
        services.AddScoped<I자동수행Coordinator, 자동수행Coordinator>();
        services.AddScoped<I수행원천Adapter, 생활배송수행원천Adapter>();
        services.AddScoped<I수행원천Adapter, 음식수행원천Adapter>();
        services.AddScoped<음식배차원천Builder>();
        services.AddScoped<I음식자동수행UseCase, 음식자동수행UseCase>();
        services.AddScoped<I자동수행Provider, 자체자동수행Provider>();
        services.AddScoped<I자동수행Provider>(sp => new 모의자동수행Provider(
            sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<TimeProvider>(),
            "mock-quick", "모의 퀵 배송", 1.15m, 25, 25));
        services.AddScoped<I자동수행Provider>(sp => new 모의자동수행Provider(
            sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<TimeProvider>(),
            "mock-local", "모의 생활 배송", .85m, 60, 10));
        foreach (var descriptor in AssumedFulfillmentProviderCatalog.Providers)
            services.AddScoped<I자동수행Provider>(sp => new 가정자동수행Provider(
                sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<TimeProvider>(), descriptor,
                sp.GetRequiredService<IOptions<FulfillmentRoutingOptions>>(),
                sp.GetRequiredService<ISsalddelExecutionModePolicy>()));
        return services;
    }
}
