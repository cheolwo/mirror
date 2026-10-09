using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Services.Development.MobileFieldTest;

public static class 모바일음식배차시험Hosting
{
    public static void Add모바일음식배차시험(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(모바일음식배차시험Options.Section).Get<모바일음식배차시험Options>() ?? new();
        if (!options.Enabled) return;
        options.Validate(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<모바일음식배차시험Guard>();
        builder.Services.AddSingleton<모바일음식배차시험자료>();
        builder.Services.AddScoped<모바일음식배차시험준비Service>();
        // 정상 경로 서비스나 Operational 관문은 바꾸지 않는다. 명시 시험 호스트의 두 adapter만 교체한다.
        builder.Services.Replace(ServiceDescriptor.Scoped<IKakao좌표변환Service, 모바일음식배차시험좌표Service>());
        builder.Services.Replace(ServiceDescriptor.Scoped<I음식배달기사제안요금Service, 모바일음식배차시험요금Service>());
        builder.Services.TryAddScoped<FoodDeliveryDriverWorkspaceUseCase>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IFoodDeliveryDriverWorkspaceUseCase, 모바일음식배차시험기사조회UseCase>());
        builder.Services.AddHostedService<모바일음식배차시험작업자>();
    }
}
