using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Services.Commerce;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection Add통신판매보호Services(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<통신판매운영Options>(configuration.GetSection(통신판매운영Options.SectionName));
        services.Configure<외부개인정보처리Options>(configuration.GetSection(외부개인정보처리Options.SectionName));
        services.TryAddSingleton<I외부개인정보처리Guard, 외부개인정보처리Guard>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<I거래신원확인Gateway, 미연결거래신원확인Gateway>();
        services.AddScoped<I판매자확인Store, Mongo판매자확인Store>();
        services.AddScoped<I거래고지증적Store, Mongo거래고지증적Store>();
        services.AddScoped<판매자확인Service>(); services.AddScoped<통신판매안내Service>();
        services.AddScoped<I통신판매거래Guard, 통신판매거래Guard>();
        return services;
    }
}
