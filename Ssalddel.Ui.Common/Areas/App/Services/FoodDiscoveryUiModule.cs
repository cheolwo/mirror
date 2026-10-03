using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Services;

internal static class FoodDiscoveryUiModule
{
    internal static IServiceCollection AddFoodDiscoveryUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<I음식배달페이지접근Service, 음식배달페이지접근Service>();
        services.TryAddScoped<I음식점탐색정책읽기Service, 음식점탐색정책Client>();
        services.TryAddScoped<I음식점공개읽기Service, 음식점공개Client>();
        services.TryAddScoped<I주문자음식주문읽기Service, 주문자음식주문Client>();
        services.TryAddScoped<I주문자음식주문쓰기Service, 주문자음식주문Client>();
        services.TryAddScoped<I주문자음식주문수령확인Service, 주문자음식주문Client>();
        services.TryAddScoped<I주문자음식주문취소Service, 주문자음식주문Client>();
        services.TryAddTransient<음식배달페이지접근ViewModel>();
        services.TryAddTransient<음식점탐색기준ViewModel>();
        services.TryAddTransient<음식점공개목록ViewModel>();
        services.TryAddTransient<음식점공개상세ViewModel>();
        services.TryAddTransient<음식주문작성ViewModel>();
        services.TryAddTransient<음식점탐색PageViewModel>();
        services.TryAddTransient<주문자음식주문목록ViewModel>();
        services.TryAddTransient<주문자음식주문상세ViewModel>();
        services.TryAddTransient<주문자음식주문취소ViewModel>();
        services.TryAddTransient<주문자음식주문PageViewModel>();

        return services;
    }
}
