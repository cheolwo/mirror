using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

internal static class HongdalUiCoreModule
{
    internal static IServiceCollection AddHongdalUiCoreModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<HongdalIsmsPClientEncryptionService>();
        services.TryAddScoped<IHongdalAccessTokenProvider, EmptyHongdalAccessTokenProvider>();
        services.TryAddScoped<IHongdal현재사용자Context, HongdalAccessToken현재사용자Context>();
        services.TryAddScoped<HongdalProtectedApiClient>();
        services.TryAddScoped<IHongdalJsonApiClient, HongdalJsonApiClient>();
        services.TryAddTransient<공통Controller기능모음ViewModel>();

        return services;
    }
}
