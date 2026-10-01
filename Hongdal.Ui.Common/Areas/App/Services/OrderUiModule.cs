using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

internal static class OrderUiModule
{
    internal static IServiceCollection AddOrderUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<I주문원장Service>(provider =>
            provider.GetRequiredService<I공동구매실행Service>());
        services.TryAddScoped<I개별주문관점Service, 개별주문관점Service>();
        services.TryAddScoped<I공동주문관점Service, 공동주문관점Service>();
        services.TryAddScoped<주문업무상태ViewModel>();
        services.TryAddScoped<주문조회ViewModel>();
        services.TryAddTransient<주문하위원장ViewModel>();
        services.TryAddTransient<주문서명ViewModel>();
        services.TryAddScoped<주문하위원장조회ViewModel>();
        services.TryAddScoped<주문하위원장연결ViewModel>();
        services.TryAddScoped<주문하위원장수정ViewModel>();
        services.TryAddScoped<주문하위원장분리ViewModel>();
        services.TryAddScoped<주문하위원장관계CrudViewModel>();
        services.TryAddScoped<주문서명상태조회ViewModel>();
        services.TryAddScoped<주문서명준비ViewModel>();
        services.TryAddScoped<주문서명등록ViewModel>();
        services.TryAddTransient<주문ViewModel>();
        services.TryAddScoped<주문자개별주문ViewModel>();
        services.TryAddScoped<판매자개별주문ViewModel>();
        services.TryAddScoped<창고관리자개별주문ViewModel>();
        services.TryAddScoped<운송담당자개별주문ViewModel>();
        services.TryAddScoped<협동조합운영자개별주문ViewModel>();
        services.TryAddScoped<개별주문PageViewModel>();
        services.TryAddScoped<주문자공동주문ViewModel>();
        services.TryAddScoped<판매자공동주문ViewModel>();
        services.TryAddScoped<창고관리자공동주문ViewModel>();
        services.TryAddScoped<운송담당자공동주문ViewModel>();
        services.TryAddScoped<협동조합운영자공동주문ViewModel>();
        services.TryAddScoped<공동주문PageViewModel>();

        return services;
    }
}
