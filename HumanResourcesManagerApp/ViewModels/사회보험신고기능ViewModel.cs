using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

public sealed class 사회보험신고기능ViewModel : 인사업무ViewModelBase
{
    private const string BasePath = "api/v1/admin/hr-social-insurance-filings";

    public 사회보험신고기능ViewModel(IHongdalJsonApiClient api)
        : base("social-insurance", "사회보험 신고", "가입 요건 평가와 신고 계획·상태를 관리합니다.")
    {
        목록조회 = 하위ViewModel등록(new Api작업ViewModel<사회보험신고조회조건, SocialInsuranceFilingPlanListResponse?>(
            (condition, cancellationToken) => api.GetAsync<SocialInsuranceFilingPlanListResponse>(
                인사Api경로.Query(BasePath,
                    ("workerUserId", condition.근로자UserId),
                    ("employerScopeType", condition.고용주범위유형),
                    ("employerScopeId", condition.고용주범위Id),
                    ("filingStatus", condition.신고상태)),
                "사회보험 신고 목록 조회",
                cancellationToken: cancellationToken)));
        상세조회 = 하위ViewModel등록(new Api작업ViewModel<Guid, SocialInsuranceFilingPlanResponse?>(
            (id, cancellationToken) => api.GetAsync<SocialInsuranceFilingPlanResponse>(
                $"{BasePath}/{id}", "사회보험 신고 상세 조회", cancellationToken: cancellationToken)));
        가입요건평가 = 하위ViewModel등록(new Api작업ViewModel<SocialInsuranceEligibilityAssessmentRequest, SocialInsuranceEligibilityAssessmentResponse?>(
            (request, cancellationToken) => api.SendAsync<SocialInsuranceEligibilityAssessmentRequest, SocialInsuranceEligibilityAssessmentResponse>(
                HttpMethod.Post, $"{BasePath}/assess", request, "사회보험 가입 요건 평가", cancellationToken: cancellationToken)));
        계획생성 = 하위ViewModel등록(new Api작업ViewModel<SocialInsuranceFilingPlanCreateRequest, SocialInsuranceFilingPlanResponse?>(
            (request, cancellationToken) => api.SendAsync<SocialInsuranceFilingPlanCreateRequest, SocialInsuranceFilingPlanResponse>(
                HttpMethod.Post, BasePath, request, "사회보험 신고 계획 생성", cancellationToken: cancellationToken)));
        상태수정 = 하위ViewModel등록(new Api작업ViewModel<사회보험신고상태수정조건, SocialInsuranceFilingPlanResponse?>(
            (condition, cancellationToken) => api.SendAsync<SocialInsuranceFilingStatusUpdateRequest, SocialInsuranceFilingPlanResponse>(
                HttpMethod.Patch,
                $"{BasePath}/{condition.Id}/status",
                condition.요청,
                "사회보험 신고 상태 수정",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<사회보험신고조회조건, SocialInsuranceFilingPlanListResponse?> 목록조회 { get; }
    public Api작업ViewModel<Guid, SocialInsuranceFilingPlanResponse?> 상세조회 { get; }
    public Api작업ViewModel<SocialInsuranceEligibilityAssessmentRequest, SocialInsuranceEligibilityAssessmentResponse?> 가입요건평가 { get; }
    public Api작업ViewModel<SocialInsuranceFilingPlanCreateRequest, SocialInsuranceFilingPlanResponse?> 계획생성 { get; }
    public Api작업ViewModel<사회보험신고상태수정조건, SocialInsuranceFilingPlanResponse?> 상태수정 { get; }
}

public sealed record 사회보험신고조회조건(
    string? 근로자UserId,
    string? 고용주범위유형,
    string? 고용주범위Id,
    string? 신고상태);

public sealed record 사회보험신고상태수정조건(Guid Id, SocialInsuranceFilingStatusUpdateRequest 요청);
