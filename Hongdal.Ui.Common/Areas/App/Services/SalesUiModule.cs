using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

internal static class SalesUiModule
{
    internal static IServiceCollection AddSalesUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<I판매채널Client, 판매채널Client>();
        services.TryAddScoped<I판매페이지Client, 판매페이지Client>();
        services.TryAddScoped<I판매채널계정Service>(provider =>
            provider.GetRequiredService<I판매채널Client>());
        services.TryAddScoped<I상품등록Service>(provider =>
            provider.GetRequiredService<I판매채널Client>());
        services.TryAddScoped<I채널출품Service>(provider =>
            provider.GetRequiredService<I판매채널Client>());
        services.TryAddScoped<판매업무상태ViewModel>();
        services.TryAddScoped<판매페이지작성ViewModel>();
        services.TryAddScoped<판매채널계정ViewModel>();
        services.TryAddScoped<상품등록ViewModel>();
        services.TryAddScoped<채널출품ViewModel>();
        services.TryAddScoped<판매채널계정조회ViewModel>();
        services.TryAddScoped<판매채널계정등록ViewModel>();
        services.TryAddScoped<판매채널계정수정ViewModel>();
        services.TryAddScoped<판매채널계정삭제ViewModel>();
        services.TryAddScoped<판매채널계정CrudViewModel>();
        services.TryAddScoped<판매상품조회ViewModel>();
        services.TryAddScoped<판매상품등록ViewModel>();
        services.TryAddScoped<판매상품수정ViewModel>();
        services.TryAddScoped<판매상품삭제ViewModel>();
        services.TryAddScoped<판매상품CrudViewModel>();
        services.TryAddScoped<채널출품조회ViewModel>();
        services.TryAddScoped<채널출품등록ViewModel>();
        services.TryAddScoped<채널출품수정ViewModel>();
        services.TryAddScoped<채널출품삭제ViewModel>();
        services.TryAddScoped<채널출품CrudViewModel>();
        services.TryAddTransient<판매ViewModel>();
        services.TryAddTransient<국내판매ViewModel>();
        services.TryAddTransient<해외수출ViewModel>();

        return services;
    }
}
