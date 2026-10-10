using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Services.Operations;

public static class 마트기사업무ServiceCollectionExtensions
{
    public static IServiceCollection Add마트기사업무(this IServiceCollection services)
    {
        services.AddOptions<마트기사업무Options>().BindConfiguration(마트기사업무Options.SectionName);
        services.TryAddScoped<I마트기사업무Gate, 마트기사업무Gate>();
        services.TryAddScoped<I마트기사업무Service, 마트기사업무Service>();
        services.TryAddScoped<I마트기사후속OutboxService, 마트기사후속OutboxService>();
        services.AddHostedService<마트기사후속OutboxWorker>();
        return services;
    }
}
