using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ui,
    HongdalModuleKind.ClientComposition,
    "일반 사용자와 운영자가 함께 쓰는 게시글 작성·수정·예약·browser 초안 기능을 조립",
    ReleaseStage = HongdalCommunityV0ReleaseStages.IndependentExecution,
    Boundary = "자료조사·LLM·유료 이미지 생성 같은 운영자 전용 도구는 포함하지 않으며 게시 등록은 서버 권한 검사를 다시 거칩니다.")]
internal static class CommunityWritingUiModule
{
    internal static IServiceCollection AddCommunityWritingUiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICommunityPostClient, CommunityPlatformClient>();
        services.TryAddScoped<ICommunityPostComposerDraftStore, BrowserCommunityPostComposerDraftStore>();
        services.TryAddTransient<CommunityPostComposerViewModel>();

        return services;
    }
}
