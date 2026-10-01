using Microsoft.Extensions.DependencyInjection.Extensions;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.ViewModels;
using WarehouseManagerApp.ViewModels.Warehouse;

namespace WarehouseManagerApp.Services;

public static class WarehouseManagerServiceCollectionExtensions
{
    public static IServiceCollection AddWarehouseManagerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<창고Controller기능모음ViewModel>();
        services.AddTransient<창고기준정보업무ViewModel>();
        services.AddTransient<창고입고업무ViewModel>();
        services.AddTransient<창고재고출고업무ViewModel>();
        services.AddTransient<창고운송연계업무ViewModel>();
        services.AddTransient<창고작업기능ViewModel>();
        services.AddTransient<창고Api기능모음ViewModel>();

        services.TryAddSingleton<IWarehouseWorkEntryGateService, SampleWarehouseWorkEntryGateService>();
        services.TryAddSingleton<IInboundReceivingWorkflowService, SampleInboundReceivingWorkflowService>();
        services.TryAddSingleton<IWarehousePickingBatchWorkspaceService, SampleWarehousePickingBatchWorkspaceService>();

        services.TryAddScoped<창고작업세션상태ViewModel>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<I창고작업구성Provider, 일반입출고작업구성Provider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<I창고작업구성Provider, 보세수입작업구성Provider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<I창고작업구성Provider, 마트도심작업구성Provider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<I창고작업구성Provider, 공동주택물류작업구성Provider>());
        services.TryAddSingleton<I창고작업구성Resolver, 창고작업구성Resolver>();

        services.AddTransient<창고홈PageViewModel>();
        services.AddTransient<창고작업보드PageViewModel>();
        services.AddTransient<창고입고예정조회PageViewModel>();
        services.AddTransient<창고작업시작PageViewModel>();
        services.AddTransient<창고작업대스캔PageViewModel>();
        services.AddTransient<창고스캔스테이션PageViewModel>();
        services.AddTransient<창고예외처리PageViewModel>();
        services.AddTransient<창고작업이력PageViewModel>();
        services.AddTransient<창고설정PageViewModel>();
        services.AddTransient<창고피킹배치PageViewModel>();

        services.AddTransient<일반입고작업PageViewModel>();
        services.AddTransient<일반재고현황PageViewModel>();
        services.AddTransient<일반출고작업PageViewModel>();
        services.AddTransient<일반운송인계PageViewModel>();

        services.AddTransient<수입화물반입PageViewModel>();
        services.AddTransient<보세통관상태PageViewModel>();
        services.AddTransient<수입화물반출PageViewModel>();
        services.AddTransient<수입국내운송인계PageViewModel>();

        services.AddTransient<마트재고보충PageViewModel>();
        services.AddTransient<마트주문처리PageViewModel>();
        services.AddTransient<마트피킹포장PageViewModel>();
        services.AddTransient<마트기사픽업PageViewModel>();

        services.AddTransient<공동주택반입예정PageViewModel>();
        services.AddTransient<공동주택입고확인PageViewModel>();
        services.AddTransient<공동주택세대배분PageViewModel>();
        services.AddTransient<공동주택수령인계PageViewModel>();
        services.AddTransient<공동주택미수령관리PageViewModel>();

        return services;
    }
}
