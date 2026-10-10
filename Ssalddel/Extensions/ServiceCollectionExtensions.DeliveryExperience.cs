using Ssalddel.Services.DeliveryExperience;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoodDeliveryExperience(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoodDeliveryExperienceOptions>(configuration.GetSection(FoodDeliveryExperienceOptions.SectionName));
        services.AddScoped<음식배달경험Service>();
        services.AddScoped<음식배달전달지역Resolver>();
        services.AddScoped<Ssalddel.Application.Driver.Settings.기사배달경험공유UseCase>();
        services.AddHostedService<음식배달경험Worker>();
        return services;
    }
}
