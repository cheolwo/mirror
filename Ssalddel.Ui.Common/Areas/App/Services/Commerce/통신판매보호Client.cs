using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;

namespace Ssalddel.Ui.Common.Areas.App.Services.Commerce;

public interface I통신판매보호Client
{
    Task<통신판매안내Response?> 안내Async(CancellationToken ct);
    Task<판매자확인Response?> 판매자Async(CancellationToken ct);
    Task<판매자확인Response?> 등록Async(판매자등록Request request, CancellationToken ct);
    Task<판매자확인Response?> 확인Async(판매자확인Request request, CancellationToken ct);
    Task<판매자공개정보Response?> 공개판매자Async(string? sellerId, long? restaurantId, CancellationToken ct);
    Task<판매자공개정보Response?> 공개거래판매자Async(long? postId, string? collaborationId, CancellationToken ct);
    Task<보호지원ListResponse> 목록Async(bool privacy, int page, CancellationToken ct);
    Task<보호지원CaseResponse?> 상세Async(bool privacy, string caseId, CancellationToken ct);
    Task<보호지원CaseResponse?> 분쟁Async(거래분쟁접수Request request, CancellationToken ct);
    Task<보호지원CaseResponse?> 권리Async(개인정보권리접수Request request, CancellationToken ct);
    Task<보호지원CaseResponse?> 변경Async(bool privacy, string caseId, 보호지원CommandRequest request, CancellationToken ct);
}

public sealed class 통신판매보호Client(ISsalddelJsonApiClient api) : I통신판매보호Client
{
    private const string Commerce = "api/v1/common/commerce";
    private static string Cases(bool privacy) => privacy ? "api/v1/common/privacy-rights-requests" : "api/v1/common/transaction-disputes";
    public Task<통신판매안내Response?> 안내Async(CancellationToken ct) => api.GetAsync<통신판매안내Response>(Commerce + "/notices", "거래 보호 안내", allowNotFound: false, cancellationToken: ct);
    public Task<판매자확인Response?> 판매자Async(CancellationToken ct) => api.GetAsync<판매자확인Response>(Commerce + "/me/seller", "내 판매자 확인", allowNotFound: false, cancellationToken: ct);
    public Task<판매자확인Response?> 등록Async(판매자등록Request request, CancellationToken ct) => api.SendAsync<판매자등록Request, 판매자확인Response>(HttpMethod.Put, Commerce + "/me/seller", request, "판매자 정보 등록", cancellationToken: ct);
    public Task<판매자확인Response?> 확인Async(판매자확인Request request, CancellationToken ct) => api.SendAsync<판매자확인Request, 판매자확인Response>(HttpMethod.Post, Commerce + "/me/seller/verification", request, "판매자 정보 확인", cancellationToken: ct);
    public Task<판매자공개정보Response?> 공개판매자Async(string? sellerId, long? restaurantId, CancellationToken ct) => api.GetAsync<판매자공개정보Response>(Commerce + (restaurantId is > 0 ? "/restaurants/" + restaurantId + "/seller/public" : "/sellers/" + Uri.EscapeDataString(sellerId ?? "") + "/public"), "판매자 공개 정보", cancellationToken: ct);
    public Task<판매자공개정보Response?> 공개거래판매자Async(long? postId, string? collaborationId, CancellationToken ct) => api.GetAsync<판매자공개정보Response>(Commerce + (!string.IsNullOrWhiteSpace(collaborationId) ? "/collaborations/" + Uri.EscapeDataString(collaborationId) : "/posts/" + postId) + "/seller/public", "거래 판매자 공개 정보", allowNotFound: false, cancellationToken: ct);
    public async Task<보호지원ListResponse> 목록Async(bool privacy, int page, CancellationToken ct) => await api.GetAsync<보호지원ListResponse>(Cases(privacy) + "?page=" + Math.Clamp(page, 1, 10000), "내 처리 요청", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException("목록 응답을 확인하지 못했습니다.");
    public Task<보호지원CaseResponse?> 상세Async(bool privacy, string caseId, CancellationToken ct) => api.GetAsync<보호지원CaseResponse>(Cases(privacy) + "/" + Uri.EscapeDataString(caseId), "요청 진행", allowNotFound: false, cancellationToken: ct);
    public Task<보호지원CaseResponse?> 분쟁Async(거래분쟁접수Request request, CancellationToken ct) => api.SendAsync<거래분쟁접수Request, 보호지원CaseResponse>(HttpMethod.Post, Cases(false), request, "거래 문제 접수", cancellationToken: ct);
    public Task<보호지원CaseResponse?> 권리Async(개인정보권리접수Request request, CancellationToken ct) => api.SendAsync<개인정보권리접수Request, 보호지원CaseResponse>(HttpMethod.Post, Cases(true), request, "개인정보 요청 접수", cancellationToken: ct);
    public Task<보호지원CaseResponse?> 변경Async(bool privacy, string caseId, 보호지원CommandRequest request, CancellationToken ct) => api.SendAsync<보호지원CommandRequest, 보호지원CaseResponse>(HttpMethod.Post, Cases(privacy) + "/" + Uri.EscapeDataString(caseId) + "/commands", request, "추가 내용 접수", cancellationToken: ct);
}

public static class 통신판매보호UiModule
{
    public static IServiceCollection AddCommerceProtectionUi(this IServiceCollection services)
    {
        services.TryAddScoped<I통신판매보호Client, 통신판매보호Client>();
        services.AddTransient<통신판매안내ViewModel>();
        services.AddTransient<판매자확인ViewModel>();
        services.AddTransient<판매자공개정보ViewModel>();
        services.AddTransient<보호지원ViewModel>();
        return services;
    }
}
