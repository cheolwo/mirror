using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

public sealed class 고용계약기능ViewModel : 인사업무ViewModelBase
{
    private const string BasePath = "api/v1/admin/hr-employment-contracts";

    public 고용계약기능ViewModel(IHongdalJsonApiClient api)
        : base("employment-contract", "고용계약", "계약 초안·서명과 급여 스케줄을 관리합니다.")
    {
        목록조회 = 하위ViewModel등록(new Api작업ViewModel<고용계약조회조건, HrEmploymentContractListResponse?>(
            (condition, cancellationToken) => api.GetAsync<HrEmploymentContractListResponse>(
                인사Api경로.Query(BasePath,
                    ("workerUserId", condition.근로자UserId),
                    ("employerScopeType", condition.고용주범위유형),
                    ("employerScopeId", condition.고용주범위Id)),
                "고용 계약 목록 조회",
                cancellationToken: cancellationToken)));
        상세조회 = 하위ViewModel등록(new Api작업ViewModel<Guid, HrEmploymentContractResponse?>(
            (contractId, cancellationToken) => api.GetAsync<HrEmploymentContractResponse>(
                $"{BasePath}/{contractId}", "고용 계약 상세 조회", cancellationToken: cancellationToken)));
        초안생성 = 하위ViewModel등록(new Api작업ViewModel<HrEmploymentContractDraftRequest, HrEmploymentContractResponse?>(
            (request, cancellationToken) => api.SendAsync<HrEmploymentContractDraftRequest, HrEmploymentContractResponse>(
                HttpMethod.Post, BasePath, request, "고용 계약 초안 생성", cancellationToken: cancellationToken)));
        서명 = 하위ViewModel등록(new Api작업ViewModel<고용계약서명조건, HrEmploymentContractResponse?>(
            (condition, cancellationToken) => api.SendAsync<HrEmploymentContractSignRequest, HrEmploymentContractResponse>(
                HttpMethod.Post,
                $"{BasePath}/{condition.계약Id}/sign",
                condition.요청,
                "고용 계약 서명",
                cancellationToken: cancellationToken)));
        급여스케줄생성 = 하위ViewModel등록(new Api작업ViewModel<급여스케줄생성조건, HrPayrollScheduleListResponse?>(
            (condition, cancellationToken) => api.SendAsync<HrPayrollScheduleCreateRequest, HrPayrollScheduleListResponse>(
                HttpMethod.Post,
                $"{BasePath}/{condition.계약Id}/payroll-schedules",
                condition.요청,
                "급여 스케줄 생성",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<고용계약조회조건, HrEmploymentContractListResponse?> 목록조회 { get; }
    public Api작업ViewModel<Guid, HrEmploymentContractResponse?> 상세조회 { get; }
    public Api작업ViewModel<HrEmploymentContractDraftRequest, HrEmploymentContractResponse?> 초안생성 { get; }
    public Api작업ViewModel<고용계약서명조건, HrEmploymentContractResponse?> 서명 { get; }
    public Api작업ViewModel<급여스케줄생성조건, HrPayrollScheduleListResponse?> 급여스케줄생성 { get; }
}

public sealed record 고용계약조회조건(string? 근로자UserId, string? 고용주범위유형, string? 고용주범위Id);
public sealed record 고용계약서명조건(Guid 계약Id, HrEmploymentContractSignRequest 요청);
public sealed record 급여스케줄생성조건(Guid 계약Id, HrPayrollScheduleCreateRequest 요청);
