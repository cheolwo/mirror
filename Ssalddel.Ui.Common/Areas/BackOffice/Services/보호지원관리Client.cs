using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.Services;

/// <summary>기존 담당자 권한·Revision·통지 증빙 API를 소비하며 외부 통지는 실행하지 않습니다.</summary>
public interface I보호지원관리Client
{
    Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct);
    Task<보호지원CaseResponse> 상세Async(string caseId, CancellationToken ct);
    Task<보호지원CaseResponse> 변경Async(string caseId, 보호지원CommandRequest request, CancellationToken ct);
    Task<보호지원증거Response> 비공개확인Async(string caseId, CancellationToken ct);
}

public sealed class 보호지원관리Client(ISsalddelJsonApiClient api) : I보호지원관리Client
{
    private const string Root = "api/v1/admin/privacy-support/cases";
    private static string Case(string id) => Root + "/" + Uri.EscapeDataString(id);
    public async Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct)
        => await api.GetAsync<보호지원ListResponse>(Root + "?kind=" + Uri.EscapeDataString(kind)
            + "&page=" + Math.Clamp(page, 1, 10000), "보호 지원 접수 목록", false, ct) ?? throw new InvalidOperationException();
    public async Task<보호지원CaseResponse> 상세Async(string caseId, CancellationToken ct)
        => await api.GetAsync<보호지원CaseResponse>(Case(caseId), "보호 지원 사건 확인", false, ct) ?? throw new InvalidOperationException();
    public async Task<보호지원CaseResponse> 변경Async(string caseId, 보호지원CommandRequest request, CancellationToken ct)
        => await api.SendAsync<보호지원CommandRequest, 보호지원CaseResponse>(HttpMethod.Post,
            Case(caseId) + "/commands", request, "보호 지원 사건 처리", cancellationToken: ct) ?? throw new InvalidOperationException();
    public async Task<보호지원증거Response> 비공개확인Async(string caseId, CancellationToken ct)
        => await api.GetAsync<보호지원증거Response>(Case(caseId) + "/evidence?reasonCode=case-review",
            "담당자 비공개 기록 확인", false, ct) ?? throw new InvalidOperationException();
}

public static class 보호지원관리UiModule
{
    public static IServiceCollection Add보호지원관리Ui(this IServiceCollection services)
    {
        services.TryAddScoped<I보호지원관리Client, 보호지원관리Client>();
        return services;
    }
}
