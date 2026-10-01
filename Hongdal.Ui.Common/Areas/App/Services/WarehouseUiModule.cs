using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

internal static class WarehouseUiModule
{
    internal static IServiceCollection AddWarehouseUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<I공동구매창고Service, 공동구매창고Service>();
        services.TryAddScoped<I입출고작업Service, 입출고작업Service>();
        services.TryAddScoped<I상차관점Service, 상차관점Service>();
        services.TryAddScoped<I하차관점Service, 하차관점Service>();
        services.TryAddScoped<I입출고원장조회Service, PlatformCommunity입출고원장조회Service>();
        services.TryAddScoped<공동구매창고상태ViewModel>();
        services.TryAddScoped<입출고화면상태ViewModel>(provider =>
            provider.GetRequiredService<공동구매창고상태ViewModel>());
        services.TryAddScoped<입출고원장상태ViewModel>();
        services.TryAddTransient<입출고원장목록ViewModel>();
        services.TryAddTransient<입고원장ViewModel>();
        services.TryAddTransient<출고원장ViewModel>();
        services.TryAddScoped<창고기준정보ViewModel>();
        services.TryAddScoped<창고목록조회ViewModel>();
        services.TryAddScoped<창고등록ViewModel>();
        services.TryAddScoped<창고수정ViewModel>();
        services.TryAddScoped<창고삭제ViewModel>();
        services.TryAddScoped<창고CrudViewModel>();
        services.TryAddScoped<창고사용자조회ViewModel>();
        services.TryAddScoped<창고사용자등록ViewModel>();
        services.TryAddScoped<창고사용자수정ViewModel>();
        services.TryAddScoped<창고사용자삭제ViewModel>();
        services.TryAddScoped<창고사용자CrudViewModel>();
        services.TryAddScoped<입고조회ViewModel>();
        services.TryAddScoped<입고예정조회ViewModel>();
        services.TryAddScoped<주문자입고예정ViewModel>();
        services.TryAddScoped<판매자입고예정ViewModel>();
        services.TryAddScoped<창고관리자입고예정ViewModel>();
        services.TryAddScoped<운송담당자입고예정ViewModel>();
        services.TryAddScoped<협동조합운영자입고예정ViewModel>();
        services.TryAddScoped<입고예정PageViewModel>();
        services.TryAddScoped<출고예정조회ViewModel>();
        services.TryAddScoped<주문자출고예정ViewModel>();
        services.TryAddScoped<판매자출고예정ViewModel>();
        services.TryAddScoped<창고관리자출고예정ViewModel>();
        services.TryAddScoped<운송담당자출고예정ViewModel>();
        services.TryAddScoped<협동조합운영자출고예정ViewModel>();
        services.TryAddScoped<출고예정PageViewModel>();
        services.TryAddScoped<상차업무상태ViewModel>();
        services.TryAddScoped<주문자상차ViewModel>();
        services.TryAddScoped<판매자상차ViewModel>();
        services.TryAddScoped<창고관리자상차ViewModel>();
        services.TryAddScoped<운송담당자상차ViewModel>();
        services.TryAddScoped<협동조합운영자상차ViewModel>();
        services.TryAddScoped<상차PageViewModel>();
        services.TryAddScoped<하차업무상태ViewModel>();
        services.TryAddScoped<주문자하차ViewModel>();
        services.TryAddScoped<판매자하차ViewModel>();
        services.TryAddScoped<창고관리자하차ViewModel>();
        services.TryAddScoped<운송담당자하차ViewModel>();
        services.TryAddScoped<협동조합운영자하차ViewModel>();
        services.TryAddScoped<하차PageViewModel>();
        services.TryAddScoped<입고등록ViewModel>();
        services.TryAddScoped<입고수정ViewModel>();
        services.TryAddScoped<입고삭제ViewModel>();
        services.TryAddScoped<입고CrudViewModel>();
        services.TryAddScoped<입고완료ViewModel>();
        services.TryAddScoped<입고재고조회ViewModel>();
        services.TryAddScoped<입고검수ViewModel>();
        services.TryAddScoped<입고적재ViewModel>();
        services.TryAddScoped<출고재고조회ViewModel>();
        services.TryAddScoped<출고포장ViewModel>();
        services.TryAddScoped<출고운송인계ViewModel>();
        services.TryAddTransient<입고ViewModel>();
        services.TryAddTransient<출고ViewModel>();
        services.TryAddTransient<입출고화면ViewModel>();
        services.TryAddTransient<공동구매창고기준정보ViewModel>();
        services.TryAddTransient<공동구매입고원장ViewModel>();
        services.TryAddTransient<공동구매출고원장ViewModel>();
        services.TryAddTransient<공동구매창고기능ViewModel>();

        return services;
    }
}
