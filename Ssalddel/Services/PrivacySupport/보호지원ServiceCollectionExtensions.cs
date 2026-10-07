using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Services.PrivacySupport;

public static class 보호지원ServiceCollectionExtensions
{
    public static IServiceCollection Add거래개인정보지원Services(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<보호지원Options>(configuration.GetSection(보호지원Options.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<I보호지원Store, Mongo보호지원Store>();
        services.AddScoped<I보호지원SourceResolver, 보호지원SourceResolver>();
        services.AddScoped<I보호지원보존연결, 보호지원보존Adapter>();
        services.AddScoped<한국영업일Calendar>();
        services.AddScoped<보호지원UseCase>();
        services.AddScoped<보호지원파기Service>();
        services.AddScoped<보호지원보존Service>();
        services.AddHostedService<보호지원파기Worker>();
        services.AddHostedService<보호지원보존Worker>();
        // 권리 요청의 실제 실행 결과 adapter는 지원하는 범위가 확인된 뒤 별도 등록합니다.
        return services;
    }
}
