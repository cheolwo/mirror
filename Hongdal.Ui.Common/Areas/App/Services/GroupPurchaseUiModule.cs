using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

internal static class GroupPurchaseUiModule
{
    internal static IServiceCollection AddGroupPurchaseUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<I공동구매업무Service, PlatformCommunity공동구매업무Service>();
        services.TryAddScoped<I공동구매공급Service, PlatformCommunity공동구매공급Service>();
        services.TryAddScoped<I공동구매물류Service, PlatformCommunity공동구매물류Service>();
        services.TryAddScoped<I공동구매실행Service, 공동구매실행Service>();
        services.TryAddScoped<I공동구매원장절차Client, 공동구매원장절차Client>();
        services.TryAddScoped<I공동수입원장전환Client, 공동수입원장전환Client>();
        services.TryAddScoped<I공동수입선적통관Client, 공동수입선적통관Client>();
        services.TryAddScoped<I공동구매가격의사결정Service, 공동구매가격의사결정Service>();
        services.TryAddScoped<IOperatingMarketProfileClient, OperatingMarketProfileClient>();
        services.TryAddScoped<공동구매화면상태ViewModel>();
        services.TryAddScoped<공동구매목록ViewModel>();
        services.TryAddScoped<공동구매거래경로판정ViewModel>();
        services.TryAddScoped<공동수입전환준비ViewModel>();
        services.TryAddScoped<공동구매거래경로분기ViewModel>();
        services.TryAddScoped<공동구매가격의사결정ViewModel>();
        services.TryAddScoped<공동구매제안ViewModel>();
        services.TryAddScoped<공동구매수요참여ViewModel>();
        services.TryAddScoped<공동구매이의검토ViewModel>();
        services.TryAddScoped<공동구매목록조회조각ViewModel>();
        services.TryAddScoped<공동구매상세조회ViewModel>();
        services.TryAddScoped<공동구매제안등록조각ViewModel>();
        services.TryAddScoped<공동구매수요참여등록ViewModel>();
        services.TryAddScoped<공동구매이의등록ViewModel>();
        services.TryAddTransient<공동구매모집기능ViewModel>();
        services.TryAddScoped<공동구매모집마감ViewModel>();
        services.TryAddScoped<공동구매결의ViewModel>();
        services.TryAddScoped<공동구매전자서명ViewModel>();
        services.TryAddScoped<공동구매모집마감처리ViewModel>();
        services.TryAddScoped<공동구매결의문등록ViewModel>();
        services.TryAddScoped<공동구매결의서명준비ViewModel>();
        services.TryAddScoped<공동구매전자서명등록ViewModel>();
        services.TryAddTransient<공동구매절차상태ViewModel>();
        services.TryAddTransient<공동구매합의기능ViewModel>();
        services.TryAddScoped<공동구매생산자연결ViewModel>();
        services.TryAddScoped<공동구매공급제안ViewModel>();
        services.TryAddScoped<공동구매공급적합성ViewModel>();
        services.TryAddScoped<공동구매협상ViewModel>();
        services.TryAddScoped<공동구매생산자후보조회ViewModel>();
        services.TryAddScoped<공동구매생산자연락요청ViewModel>();
        services.TryAddScoped<공동구매대표후보조회ViewModel>();
        services.TryAddScoped<공동구매공급제안등록ViewModel>();
        services.TryAddScoped<공동구매공급적합성미리보기ViewModel>();
        services.TryAddScoped<공동구매협상이력조회ViewModel>();
        services.TryAddScoped<공동구매협상이벤트등록ViewModel>();
        services.TryAddScoped<공동구매협상쟁점등록ViewModel>();
        services.TryAddScoped<공동구매숙고의견등록ViewModel>();
        services.TryAddScoped<공동구매협상쟁점합의ViewModel>();
        services.TryAddTransient<공동구매공급기능ViewModel>();
        services.TryAddScoped<공동구매이행계획ViewModel>();
        services.TryAddScoped<공동구매이행계획미리보기ViewModel>();
        services.TryAddScoped<공동구매발주초안등록ViewModel>();
        services.TryAddTransient<공동구매물류기능ViewModel>();
        services.TryAddTransient<국내공동구매분기ViewModel>();
        services.TryAddScoped<공동수입원장물류ViewModel>();
        services.TryAddScoped<공동수입선적통관ViewModel>();
        services.TryAddScoped<공동수입원장조회ViewModel>();
        services.TryAddScoped<공동수입원장미리보기ViewModel>();
        services.TryAddScoped<공동수입원장전환ViewModel>();
        services.TryAddScoped<공동수입선적공개조회ViewModel>();
        services.TryAddScoped<공동수입선적관리목록조회ViewModel>();
        services.TryAddScoped<공동수입선적등록ViewModel>();
        services.TryAddScoped<공동수입선적이벤트등록ViewModel>();
        services.TryAddScoped<공동수입통관동기화ViewModel>();
        services.TryAddTransient<공동수입분기ViewModel>();
        services.TryAddScoped<공동구매실행상태ViewModel>();
        services.TryAddScoped<공동구매자동집단ViewModel>();
        services.TryAddScoped<공동구매자동집단조회ViewModel>();
        services.TryAddScoped<공동구매자동수요등록ViewModel>();
        services.TryAddTransient<공동구매주문집계ViewModel>();
        services.TryAddTransient<공동구매재고배분ViewModel>();
        services.TryAddScoped<공동구매주문원장조회ViewModel>();
        services.TryAddScoped<공동구매하위원장ViewModel>();
        services.TryAddScoped<공동구매주문원장서명ViewModel>();
        services.TryAddScoped<공동구매주문원장상세조회ViewModel>();
        services.TryAddScoped<공동구매주문하위원장연결ViewModel>();
        services.TryAddScoped<공동구매주문하위원장분리ViewModel>();
        services.TryAddScoped<공동구매주문서명상태조회ViewModel>();
        services.TryAddScoped<공동구매주문서명준비ViewModel>();
        services.TryAddScoped<공동구매주문서명등록ViewModel>();
        services.TryAddTransient<공동구매주문원장ViewModel>();
        services.TryAddScoped<공동구매커머스이행ViewModel>();
        services.TryAddScoped<공동구매커머스이행조회ViewModel>();
        services.TryAddScoped<공동구매커머스문서조회ViewModel>();
        services.TryAddTransient<공동구매실행기능ViewModel>();
        services.TryAddTransient<공동구매화면ViewModel>();

        return services;
    }
}
