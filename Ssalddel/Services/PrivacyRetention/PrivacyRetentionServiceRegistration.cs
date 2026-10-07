using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Services.PrivacyRetention;

public static class 개인정보보존ServiceRegistration
{
    public static IServiceCollection Add개인정보보존Services(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<개인정보보존Options>(configuration.GetSection(개인정보보존Options.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<I개인정보보존Store, Mongo개인정보보존Store>();
        services.AddSingleton<I개인정보접속점검Service, 개인정보접속점검Service>();
        services.AddScoped<I배송원장개인정보파기Service, 배송원장개인정보파기Service>();
        services.AddScoped<I개인정보파기Adapter, 음식배송개인정보파기Adapter>();
        services.AddScoped<I개인정보파기Adapter, 생활배송개인정보파기Adapter>();
        services.AddScoped<I개인정보파기Adapter, 접속감사개인정보파기Adapter>();
        services.AddScoped<I개인정보파기Adapter, 기사위치개인정보파기Adapter>();
        services.AddScoped<개인정보보존조정Service>();
        services.AddScoped<I개인정보보존조정Service>(sp => sp.GetRequiredService<개인정보보존조정Service>());
        services.AddSingleton<I개인정보복원차단Service, 개인정보복원차단Service>();
        services.AddHostedService<개인정보파기Worker>();
        return services;
    }
}
