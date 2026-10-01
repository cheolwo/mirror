using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

public sealed class 인사역할기능ViewModel : 인사업무ViewModelBase
{
    private const string BasePath = "api/v1/admin/hr-roles";

    public 인사역할기능ViewModel(IHongdalJsonApiClient api)
        : base("hr-role", "인사 역할", "사용자의 인사 역할과 적용 범위를 관리합니다.")
    {
        목록조회 = 하위ViewModel등록(new Api작업ViewModel<인사역할조회조건, HrRoleAssignmentListResponse?>(
            (condition, cancellationToken) => api.GetAsync<HrRoleAssignmentListResponse>(
                인사Api경로.Query(BasePath,
                    ("userId", condition.UserId),
                    ("scopeType", condition.범위유형),
                    ("scopeId", condition.범위Id)),
                "인사 역할 목록 조회",
                cancellationToken: cancellationToken)));
        배정 = 하위ViewModel등록(new Api작업ViewModel<HrRoleAssignmentRequest, HrRoleAssignmentResponse?>(
            (request, cancellationToken) => api.SendAsync<HrRoleAssignmentRequest, HrRoleAssignmentResponse>(
                HttpMethod.Post, BasePath, request, "인사 역할 배정", cancellationToken: cancellationToken)));
        해제 = 하위ViewModel등록(new Api작업ViewModel<Guid, Api작업완료>(async (assignmentId, cancellationToken) =>
        {
            await api.SendAsync(HttpMethod.Delete, $"{BasePath}/{assignmentId}", "인사 역할 해제", cancellationToken);
            return Api작업완료.값;
        }));
    }

    public Api작업ViewModel<인사역할조회조건, HrRoleAssignmentListResponse?> 목록조회 { get; }
    public Api작업ViewModel<HrRoleAssignmentRequest, HrRoleAssignmentResponse?> 배정 { get; }
    public Api작업ViewModel<Guid, Api작업완료> 해제 { get; }
}

public sealed record 인사역할조회조건(string? UserId, string? 범위유형, string? 범위Id);
